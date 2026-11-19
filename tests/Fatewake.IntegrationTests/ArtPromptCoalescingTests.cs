using Fatewake.Api;
using Fatewake.Infrastructure.Art;
using Fatewake.Infrastructure.Persistence;
using Fatewake.Infrastructure.Work;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Fatewake.IntegrationTests;

/// <summary>Verifies concurrent prompt misses coalesce through PostgreSQL's durable job identity.</summary>
/// <see href="../../docs/code/tests/Fatewake.IntegrationTests/ArtPromptCoalescingTests.md">Documentation</see>
public sealed class ArtPromptCoalescingTests
{
    /// <summary>Concurrent misses for one canonical scene layer share one durable authoring job.</summary>
    [Fact]
    public async Task Concurrent_prompt_misses_share_one_durable_authoring_job()
    {
        var connection = Environment.GetEnvironmentVariable("FATEWAKE_ART_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection))
            Assert.Skip("A disposable PostgreSQL artwork test database is required.");
        var ct = TestContext.Current.CancellationToken;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ArtGeneration:Provider"] = "None",
            ["ArtPromptAuthoring:Enabled"] = "true",
            ["ArtPromptAuthoring:ApiKey"] = "fake-token"
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddDbContext<FatewakeDbContext>(options => options.UseNpgsql(connection));
        services.AddSingleton<SceneArtCatalog>();
        services.AddScoped<ArtPromptJobFactory>();
        services.AddScoped<SceneArtPromptService>();
        services.AddScoped<IWorkJobBuilder, WorkJobBuilder>();
        services.AddSingleton<IWorkSignalBus, NullWorkSignalBus>();
        await using var provider = services.BuildServiceProvider();

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FatewakeDbContext>();
            await DatabaseInitializer.InitializeAsync(db, ct);
            await new ArtPromptSeedImporter(db, provider.GetRequiredService<SceneArtCatalog>(),
                scope.ServiceProvider.GetRequiredService<ILogger<ArtPromptSeedImporter>>()).InitializeAsync(ct);
            var prompt = await db.Set<ArtPromptRecord>().SingleAsync(row =>
                row.AssetKey == "light-day1-after-shutdown", ct);
            prompt.Status = "disabled";
            prompt.FailureCode = null;
            prompt.AuthoringJobId = null;
            await db.SaveChangesAsync(ct);
            await db.WorkJobs.Where(row => row.JobType == "art.prompt" &&
                row.IdempotencyKey == prompt.IdentityKey).ExecuteDeleteAsync(ct);
        }

        try
        {
            async Task<SceneArtPromptResolution> ResolveInScopeAsync()
            {
                using var scope = provider.CreateScope();
                return await scope.ServiceProvider.GetRequiredService<SceneArtPromptService>()
                    .ResolveAsync("light-day1-after-shutdown", null, null, ct: ct);
            }

            var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => ResolveInScopeAsync()));
            Assert.All(results, result => Assert.Equal("pending", result.Status));
            Assert.Single(results.Select(result => result.JobId).Distinct());
            Assert.All(results, result => Assert.NotNull(result.JobId));
            using var verification = provider.CreateScope();
            var verificationDb = verification.ServiceProvider.GetRequiredService<FatewakeDbContext>();
            var prompt = await verificationDb.Set<ArtPromptRecord>().SingleAsync(row =>
                row.AssetKey == "light-day1-after-shutdown", ct);
            Assert.Equal("pending", prompt.Status);
            Assert.Equal(results[0].JobId, prompt.AuthoringJobId);
            Assert.Equal(1, await verificationDb.WorkJobs.CountAsync(row =>
                row.JobType == "art.prompt" && row.IdempotencyKey == prompt.IdentityKey, ct));
            var step = await verificationDb.WorkSteps.SingleAsync(row => row.JobId == prompt.AuthoringJobId, ct);
            Assert.Equal(ArtWorkQueues.Prompt, step.Queue);
            Assert.DoesNotContain("future", step.Input, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            using var cleanup = provider.CreateScope();
            var db = cleanup.ServiceProvider.GetRequiredService<FatewakeDbContext>();
            var prompt = await db.Set<ArtPromptRecord>().SingleAsync(row =>
                row.AssetKey == "light-day1-after-shutdown", ct);
            var jobId = prompt.AuthoringJobId;
            prompt.Status = "ready";
            prompt.AuthoringJobId = null;
            prompt.FailureCode = null;
            await db.SaveChangesAsync(ct);
            if (jobId is { } id)
                await db.WorkJobs.Where(row => row.Id == id).ExecuteDeleteAsync(ct);
        }
    }
}
