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
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Fatewake.IntegrationTests;

/// <summary>Verifies concurrent scene requests through the real PostgreSQL artwork pipeline.</summary>
public sealed class SceneArtworkPipelineTests
{
    /// <summary>A missing scene is queued once, generated, finalized, and served without paid provider calls.</summary>
    [Fact]
    public async Task Concurrent_requests_generate_one_asset_and_deliver_completed_artwork()
    {
        var connection = Environment.GetEnvironmentVariable("FATEWAKE_ART_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) Assert.Skip("An isolated artwork test database is required.");
        var ct = TestContext.Current.CancellationToken;
        var root = Path.Combine(Path.GetTempPath(), "fatewake-art-test-" + Guid.NewGuid().ToString("N"));
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ArtGeneration:Provider"] = "Local", ["ArtGeneration:Local:Enabled"] = "true"
        });
        builder.Services.AddDbContext<FatewakeDbContext>(options => options.UseNpgsql(connection));
        builder.Services.AddSingleton<SceneArtCatalog>();
        builder.Services.AddScoped<IArtAssetStore, ArtAssetStore>();
        builder.Services.AddScoped<IArtResolutionService, ArtResolutionService>();
        builder.Services.AddScoped<IWorkJobBuilder, WorkJobBuilder>();
        builder.Services.AddScoped<IArtWorkJobFactory, ArtWorkJobFactory>();
        builder.Services.AddScoped<IWorkStore, WorkStore>();
        builder.Services.AddSingleton<IWorkSignalBus, NullWorkSignalBus>();
        builder.Services.AddSingleton<IArtBinaryStorage>(new FileSystemArtBinaryStorage(root));
        builder.Services.AddScoped<IWorkArtifactStore, WorkArtifactStore>();
        builder.Services.AddScoped<IArtGenerationStore, ArtGenerationStore>();
        builder.Services.AddSingleton<IArtImageProcessor, ImageSharpArtImageProcessor>();
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
        using (var scope = app.Services.CreateScope())
            await DatabaseInitializer.InitializeAsync(scope.ServiceProvider.GetRequiredService<FatewakeDbContext>(), ct);
        await app.StartAsync(ct);
        using var client = app.GetTestClient();
        try
        {
            var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => client.PostAsJsonAsync(
                "/api/art/scene/resolve", new { keys = new[] { "bg-day1-bedroom-dark" } }, ct)));
            foreach (var response in responses) { response.EnsureSuccessStatusCode(); response.Dispose(); }
            using (var scope = app.Services.CreateScope())
                Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<FatewakeDbContext>().WorkJobs.CountAsync(ct));

            for (var step = 0; step < 7; step++)
            {
                using var scope = app.Services.CreateScope();
                var store = scope.ServiceProvider.GetRequiredService<IWorkStore>();
                WorkLease? lease = null;
                foreach (var queue in new[] { ArtWorkQueues.Generate, ArtWorkQueues.Encode, ArtWorkQueues.Validate, ArtWorkQueues.Finalize })
                {
                    lease = await store.ClaimAsync(queue, "scene-art-test", TimeSpan.FromMinutes(1), ct);
                    if (lease is not null) break;
                }
                Assert.NotNull(lease);
                var handler = scope.ServiceProvider.GetServices<IWorkStepHandler>().Single(x => x.StepType == lease.StepType);
                var output = await handler.ExecuteAsync(new(lease.JobId, lease.StepId, lease.Input, lease.Attempt), ct);
                Assert.True(await store.CompleteAsync(lease.StepId, lease.LeaseToken, output, ct));
            }

            Assert.Equal(1, app.Services.GetRequiredService<Generator>().Calls);
            using var resolved = await client.PostAsJsonAsync("/api/art/scene/resolve", new { keys = new[] { "bg-day1-bedroom-dark" } }, ct);
            var asset = Assert.Single((await resolved.Content.ReadFromJsonAsync<SceneArtResult[]>(ct))!);
            Assert.Equal("ready", asset.Status);
            foreach (var format in new[] { "png", "webp" })
            {
                using var response = await client.GetAsync($"/api/art/{asset.Key}/{asset.Version}/{format}", ct);
                response.EnsureSuccessStatusCode();
                using var image = Image.Load(await response.Content.ReadAsByteArrayAsync(ct));
                Assert.Equal(32, image.Width);
                Assert.Equal(48, image.Height);
            }
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private sealed class Generator : IArtMasterGenerator
    {
        public int Calls;
        public Task<GeneratedArtMaster> GenerateAsync(ArtGenerationInvocation invocation, CancellationToken ct = default)
        {
            Calls++;
            using var image = new Image<Rgba32>(32, 48, new Rgba32(100, 80, 60, 255));
            using var stream = new MemoryStream();
            image.SaveAsPng(stream);
            return Task.FromResult(new GeneratedArtMaster(stream.ToArray(), "test-provider", 0));
        }
    }
}
