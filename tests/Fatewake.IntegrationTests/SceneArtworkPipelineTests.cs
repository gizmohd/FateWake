using System.Net.Http.Json;
using Fatewake.Api;
using Fatewake.Infrastructure.Art;
using Fatewake.Infrastructure.Persistence;
using Fatewake.Infrastructure.Work;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;
using System.Text.Json;

namespace Fatewake.IntegrationTests;

/// <summary>Verifies concurrent scene requests through the real PostgreSQL artwork pipeline.</summary>
/// <see href="../../docs/code/tests/Fatewake.IntegrationTests/SceneArtworkPipelineTests.md">Documentation</see>
public sealed class SceneArtworkPipelineTests
{
    /// <summary>A missing scene is queued once, generated, finalized, and served without paid provider calls.</summary>
    [Fact]
    public async Task Concurrent_requests_generate_one_asset_and_deliver_completed_artwork()
    {
        var connection = Environment.GetEnvironmentVariable("FATEWAKE_ART_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection))
            Assert.Skip("An isolated artwork test database is required.");
        var ct = TestContext.Current.CancellationToken;
        var root = Path.GetFullPath(Path.Combine("art", ".art-test-storage-" + Guid.NewGuid().ToString("N")));
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ArtGeneration:Provider"] = "Local",
            ["ArtGeneration:Local:Enabled"] = "true"
        });
        builder.Services.AddDbContext<FatewakeDbContext>(options => options.UseNpgsql(connection));
        builder.Services.AddSingleton<SceneArtCatalog>();
        builder.Services.AddScoped<IArtAssetStore, ArtAssetStore>();
        builder.Services.AddScoped<IArtResolutionService, ArtResolutionService>();
        builder.Services.AddScoped<IWorkJobBuilder, WorkJobBuilder>();
        builder.Services.AddScoped<IArtWorkJobFactory, ArtWorkJobFactory>();
        builder.Services.AddScoped<ArtPromptJobFactory>();
        builder.Services.AddScoped<SceneArtPromptService>();
        builder.Services.AddScoped<ArtPromptSeedImporter>();
        builder.Services.AddScoped<IWorkStore, WorkStore>();
        builder.Services.AddSingleton<IWorkSignalBus, NullWorkSignalBus>();
        builder.Services.AddSingleton<IArtBinaryStorage>(new FileSystemArtBinaryStorage(root));
        builder.Services.AddScoped<IWorkArtifactStore, WorkArtifactStore>();
        builder.Services.AddScoped<IArtGenerationStore, ArtGenerationStore>();
        builder.Services.AddSingleton<IArtImageProcessor, SkiaSharpArtImageProcessor>();
        builder.Services.AddSingleton<Generator>();
        builder.Services.AddSingleton<IArtMasterGenerator>(sp => sp.GetRequiredService<Generator>());
        builder.Services.AddScoped<IWorkStepHandler, ArtResolveWorkStepHandler>();
        builder.Services.AddScoped<IWorkStepHandler, ArtGenerateMasterWorkStepHandler>();
        builder.Services.AddScoped<IWorkStepHandler, ArtEncodeWebPWorkStepHandler>();
        builder.Services.AddScoped<IWorkStepHandler, ArtOptimizePngWorkStepHandler>();
        builder.Services.AddScoped<IWorkStepHandler, ArtInspectMetadataWorkStepHandler>();
        builder.Services.AddScoped<IWorkStepHandler, ArtValidateWorkStepHandler>();
        builder.Services.AddScoped<IWorkStepHandler, ArtFinalizeWorkStepHandler>();
        await using var app = builder.Build();
        app.MapSceneArt();
        PromptRevisionBinding promptBinding;
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FatewakeDbContext>();
            await DatabaseInitializer.InitializeAsync(db, ct);
            await scope.ServiceProvider.GetRequiredService<ArtPromptSeedImporter>().InitializeAsync(ct);
            promptBinding = await PinUniquePromptRevisionAsync(db, "bg-day1-bedroom-dark", ct);
        }
        await app.StartAsync(ct);
        using var client = app.GetTestClient();
        try
        {
            var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => client.PostAsJsonAsync(
                "/api/art/scene/resolve", new
                {
                    keys = new[] { "bg-day1-bedroom-dark" }
                }, ct)));
            var results = new List<SceneArtResult>();
            foreach (var response in responses)
            {
                using (response)
                {
                    response.EnsureSuccessStatusCode();
                    var body = await response.Content.ReadAsStringAsync(ct);
                    Assert.DoesNotContain("resolvedPrompt", body, StringComparison.OrdinalIgnoreCase);
                    Assert.DoesNotContain("positivePrompt", body, StringComparison.OrdinalIgnoreCase);
                    results.Add(Assert.Single(JsonSerializer.Deserialize<SceneArtResult[]>(body,
                        new JsonSerializerOptions(JsonSerializerDefaults.Web))!));
                }
            }
            Assert.All(results, result => Assert.Equal("pending", result.Status));
            var targetJobId = results[0].JobId ?? throw new InvalidOperationException("The scene request did not return its durable job ID.");
            Assert.All(results, result => Assert.Equal(targetJobId, result.JobId));

            int stepCount;
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<FatewakeDbContext>();
                var workJob = await db.WorkJobs.SingleAsync(row => row.Id == targetJobId, ct);
                Assert.Equal("art.asset", workJob.JobType);
                stepCount = await db.WorkSteps.CountAsync(row => row.JobId == targetJobId, ct);
                Assert.Equal(7, stepCount);
            }

            for (var step = 0; step < stepCount; step++)
            {
                using var scope = app.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<FatewakeDbContext>();
                var store = scope.ServiceProvider.GetRequiredService<IWorkStore>();
                var lease = await ClaimNextJobStepAsync(db, targetJobId, ct);
                Assert.NotNull(lease);
                var handler = scope.ServiceProvider.GetServices<IWorkStepHandler>().Single(x => x.StepType == lease.StepType);
                var output = await handler.ExecuteAsync(new(lease.JobId, lease.StepId, lease.Input, lease.Attempt), ct);
                Assert.True(await store.CompleteAsync(lease.StepId, lease.LeaseToken, output, ct));
            }

            Assert.Equal(1, app.Services.GetRequiredService<Generator>().Calls);
            using var resolved = await client.PostAsJsonAsync("/api/art/scene/resolve", new
            {
                keys = new[] { "bg-day1-bedroom-dark" }
            }, ct);
            var readyBody = await resolved.Content.ReadAsStringAsync(ct);
            Assert.DoesNotContain("resolvedPrompt", readyBody, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("positivePrompt", readyBody, StringComparison.OrdinalIgnoreCase);
            var asset = Assert.Single(JsonSerializer.Deserialize<SceneArtResult[]>(readyBody,
                new JsonSerializerOptions(JsonSerializerDefaults.Web))!);
            Assert.Equal("ready", asset.Status);
            foreach (var format in new[] { "png", "webp" })
            {
                using var response = await client.GetAsync($"/api/art/{asset.Key}/{asset.Version}/{format}", ct);
                response.EnsureSuccessStatusCode();
                using var image = SKBitmap.Decode(await response.Content.ReadAsByteArrayAsync(ct));
                Assert.NotNull(image);
                Assert.Equal(32, image.Width);
                Assert.Equal(48, image.Height);
            }
        }
        finally
        {
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<FatewakeDbContext>();
                var prompt = await db.Set<ArtPromptRecord>().SingleAsync(
                    row => row.AssetKey == "bg-day1-bedroom-dark", ct);
                prompt.CurrentRevisionId = promptBinding.CurrentRevisionId;
                prompt.CurrentRevisionNumber = promptBinding.CurrentRevisionNumber;
                prompt.LatestRevisionId = promptBinding.LatestRevisionId;
                prompt.LatestRevisionNumber = promptBinding.LatestRevisionNumber;
                prompt.UpdatedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(ct);
            }
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    private static async Task<PromptRevisionBinding> PinUniquePromptRevisionAsync(FatewakeDbContext db,
        string assetKey, CancellationToken ct)
    {
        var prompt = await db.Set<ArtPromptRecord>().SingleAsync(row => row.AssetKey == assetKey, ct);
        var parent = await db.Set<ArtPromptRevisionRecord>().SingleAsync(row =>
            row.Id == prompt.LatestRevisionId, ct);
        var binding = new PromptRevisionBinding(prompt.CurrentRevisionId, prompt.CurrentRevisionNumber,
            prompt.LatestRevisionId, prompt.LatestRevisionNumber);
        var revisionNumber = 1_000_000 + Random.Shared.Next(1_000_000);
        while (await db.Set<ArtPromptRevisionRecord>().AnyAsync(row =>
                   row.ArtPromptId == prompt.Id && row.RevisionNumber == revisionNumber, ct))
            revisionNumber++;
        var now = DateTimeOffset.UtcNow;
        var revision = new ArtPromptRevisionRecord
        {
            Id = Guid.NewGuid(),
            ArtPromptId = prompt.Id,
            RevisionNumber = revisionNumber,
            ParentRevisionId = parent.Id,
            PositivePrompt = parent.PositivePrompt,
            NegativePrompt = parent.NegativePrompt,
            Source = "ai",
            Model = "integration-test",
            RequestContextFingerprint = prompt.ContextFingerprint,
            CreatedAt = now
        };
        db.Add(revision);
        prompt.CurrentRevisionId = revision.Id;
        prompt.CurrentRevisionNumber = revision.RevisionNumber;
        prompt.LatestRevisionId = revision.Id;
        prompt.LatestRevisionNumber = revision.RevisionNumber;
        prompt.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        return binding;
    }

    private static async Task<WorkLease?> ClaimNextJobStepAsync(FatewakeDbContext db, Guid jobId,
        CancellationToken ct)
    {
        var step = await db.WorkSteps
            .Where(row => row.JobId == jobId && row.Status == WorkStepStatus.Ready)
            .OrderByDescending(row => row.Priority)
            .ThenBy(row => row.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (step is null)
            return null;
        var now = DateTimeOffset.UtcNow;
        var token = Guid.NewGuid();
        step.Status = WorkStepStatus.Leased;
        step.LeaseOwner = "scene-art-test";
        step.LeaseToken = token;
        step.LeaseExpiresAt = now + TimeSpan.FromMinutes(1);
        step.LastHeartbeatAt = now;
        step.AttemptCount++;
        step.UpdatedAt = now;
        var job = await db.WorkJobs.SingleAsync(row => row.Id == jobId, ct);
        if (job.Status == WorkJobStatus.Pending)
            job.Status = WorkJobStatus.Running;
        job.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        return new WorkLease(step.Id, step.JobId, step.StepType, step.Queue, step.Input, token, step.AttemptCount);
    }

    private sealed class Generator : IArtMasterGenerator
    {
        public int Calls;

        public Task<GeneratedArtMaster> GenerateAsync(ArtGenerationInvocation invocation, CancellationToken ct = default)
        {
            Calls++;
            using var bitmap = new SKBitmap(32, 48);
            bitmap.Erase(new SKColor(100, 80, 60));
            using var png = bitmap.Encode(SKEncodedImageFormat.Png, 100);
            return Task.FromResult(new GeneratedArtMaster(png.ToArray(), "test-provider", 0));
        }
    }

    private sealed record PromptRevisionBinding(Guid? CurrentRevisionId, int? CurrentRevisionNumber,
        Guid? LatestRevisionId, int? LatestRevisionNumber);
}
