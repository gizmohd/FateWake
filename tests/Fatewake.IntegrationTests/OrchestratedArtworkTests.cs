using Fatewake.Infrastructure.Art;
using Fatewake.Infrastructure.Persistence;
using Fatewake.Infrastructure.Work;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Fatewake.IntegrationTests;

/// <summary>Exercises the running AppHost's worker through the complete approved-artwork reuse pipeline.</summary>
/// <see href="../../docs/code/tests/Fatewake.IntegrationTests/OrchestratedArtworkTests.md">OrchestratedArtworkTests documentation</see>
public sealed class OrchestratedArtworkTests
{
    /// <summary>The running worker completes all seven steps, including finalization, without a generation provider.</summary>
    [Fact]
    public async Task Running_worker_completes_approved_artwork_reuse()
    {
        var connection = Environment.GetEnvironmentVariable("FATEWAKE_ORCHESTRATION_CONNECTION");
        var root = Environment.GetEnvironmentVariable("FATEWAKE_ORCHESTRATION_ART_ROOT");
        var broker = Environment.GetEnvironmentVariable("FATEWAKE_TEST_RABBITMQ");
        if (string.IsNullOrWhiteSpace(connection) || string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(broker))
            Assert.Skip("A running AppHost, database connection, shared art root, and broker connection are required.");
        var ct = TestContext.Current.CancellationToken;
        var options = new DbContextOptionsBuilder<FatewakeDbContext>().UseNpgsql(connection).Options;
        await using var db = new FatewakeDbContext(options);
        var key = $"orchestration-test-{Guid.NewGuid():N}";
        var storage = new FileSystemArtBinaryStorage(root);
        using var fixture = new Image<Rgba32>(32, 24, new Rgba32(100, 80, 60, 255));
        using var png = new MemoryStream();
        fixture.SaveAsPng(png);
        var provenance = new ArtGenerationProvenance("test-fixture", null, null, "Test fixture", null,
            "test", "test", null, null, null, [], new Dictionary<string, object?>(), new Dictionary<string, string>(), 0);
        var asset = await new ArtAssetIngestionService(db, storage, new ImageSharpArtImageProcessor())
            .IngestAsync(new ArtIngestRequest(key, "HeroIllustration", key, png.ToArray(), provenance, Approve: true), ct);
        await using var signals = new RabbitMqWorkSignalBus(Options.Create(new RabbitMqWorkOptions { ConnectionString = broker }));
        Guid? jobId = null;
        try
        {
            var jobs = new WorkJobBuilder(db, signals, NullLogger<WorkJobBuilder>.Instance);
            var job = await new ArtWorkJobFactory(jobs).CreateAsync(new ArtWorkRequest(key, "HeroIllustration", key,
                "Must reuse the approved fixture", "None", "None"), ct);
            jobId = job.JobId;
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(TimeSpan.FromSeconds(30));
            WorkJobStatus status;
            do
            {
                await Task.Delay(100, deadline.Token);
                status = await db.WorkJobs.AsNoTracking().Where(x => x.Id == job.JobId).Select(x => x.Status).SingleAsync(deadline.Token);
            } while (status is not WorkJobStatus.Completed and not WorkJobStatus.Failed);

            Assert.Equal(WorkJobStatus.Completed, status);
            var steps = await db.WorkSteps.AsNoTracking().Where(x => x.JobId == job.JobId).ToListAsync(ct);
            Assert.Equal(7, steps.Count);
            Assert.All(steps, step => Assert.Equal(WorkStepStatus.Completed, step.Status));
            Assert.Contains(steps, step => step.Queue == ArtWorkQueues.Finalize);
            Assert.Contains(await db.WorkArtifacts.Where(x => x.JobId == job.JobId).ToListAsync(ct),
                artifact => artifact.Key == "art.finalize");
            Assert.Empty(await db.ArtGenerations.Where(x => x.WorkJobId == job.JobId).ToListAsync(ct));
            Assert.Equal(asset.AssetId, (await db.ArtAssets.AsNoTracking().SingleAsync(x => x.VisualFingerprint == key, ct)).Id);
        }
        finally
        {
            if (jobId is not null)
            {
                var steps = db.WorkSteps.Where(x => x.JobId == jobId).Select(x => x.Id);
                await db.WorkStepDependencies.Where(x => steps.Contains(x.StepId) || steps.Contains(x.DependsOnStepId))
                    .ExecuteDeleteAsync(ct);
                await db.WorkJobs.Where(x => x.Id == jobId).ExecuteDeleteAsync(ct);
            }
            await db.ArtGenerations.Where(x => x.ArtAssetId == asset.AssetId).ExecuteDeleteAsync(ct);
            await db.ArtAssets.Where(x => x.Id == asset.AssetId).ExecuteDeleteAsync(ct);
            var fixtureDirectory = Path.Combine(root, "art", key);
            if (Directory.Exists(fixtureDirectory)) Directory.Delete(fixtureDirectory, recursive: true);
        }
    }
}
