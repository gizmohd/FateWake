using System.Collections.Concurrent;
using System.Text;
using Fatewake.Infrastructure.Persistence;
using Fatewake.Infrastructure.Rag;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace Fatewake.IntegrationTests;

/// <summary>Verifies claim review and RAG work coordination against PostgreSQL.</summary>
public sealed class RagClaimWorkflowTests
{
    /// <summary>Covers import replay, source review decisions, and disjoint worker reservations.</summary>
    [Fact]
    public async Task Parallel_workers_process_each_chunk_once_and_only_approved_claims_are_retrievable()
    {
        var connection = Environment.GetEnvironmentVariable("FATEWAKE_TEST_CONNECTION");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(connection), "Requires an isolated PostgreSQL database.");
        var settings = new NpgsqlConnectionStringBuilder(connection) { Database = $"fatewake_claims_{Guid.NewGuid():N}" };
        var options = new DbContextOptionsBuilder<FatewakeDbContext>().UseNpgsql(settings.ConnectionString).Options;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(40));
        var ct = timeout.Token;
        await using var first = new FatewakeDbContext(options);
        await using var second = new FatewakeDbContext(options);
        try
        {
            await first.Database.MigrateAsync(ct);
            var import = new RagIngestionService(first);
            var otherImport = new RagIngestionService(second);
            var gate = Request("gate", "East Gate closes at sundown.", 1);
            var staging = new RagStagingStore(first);
            var otherStaging = new RagStagingStore(second);
            var staged = await Task.WhenAll(
                staging.PutAsync("gate", "test-content", gate.FileName, gate.ContentType, gate.Bytes, ct),
                otherStaging.PutAsync("gate", "test-content", "renamed.txt", gate.ContentType, gate.Bytes, ct));
            Assert.Equal(staged[0], staged[1]);
            var otherStaged = await otherStaging.PutAsync("other-source", "test-content", gate.FileName,
                gate.ContentType, gate.Bytes, ct);
            Assert.NotEqual(staged[0], otherStaged);
            await staging.DeleteAsync(staged[0], ct);
            Assert.NotNull(await otherStaging.GetAsync(otherStaged, ct));
            var imports = await Task.WhenAll(import.ImportAsync(gate, ct), otherImport.ImportAsync(gate, ct));
            Assert.Equal(imports[0].DocumentId, imports[1].DocumentId);
            Assert.Single(imports, x => x.Reused);
            var road = await otherImport.ImportAsync(Request("road", "North Road leads to the ridge.", 3) with
            {
                ReviewStatus = RagClaimService.Pending
            }, ct);

            var firstClaims = new RagClaimService(first);
            var secondClaims = new RagClaimService(second);
            var eligibleSources = await firstClaims.ListSourcesAsync(null, ct);
            Assert.Equal(2, eligibleSources.Length);
            Assert.True(await firstClaims.SetPolicyAsync(road.SourceId, 0.9m, ct));

            var extractor = new ParallelExtractor();
            var extracted = await Task.WhenAll(firstClaims.ExtractNextAsync(extractor, ct),
                secondClaims.ExtractNextAsync(extractor, ct));
            Assert.All(extracted, Assert.True);
            Assert.Equal(2, extractor.Passages.Distinct().Count());
            Assert.False(await firstClaims.ExtractNextAsync(extractor, ct));

            var filter = new RagSearchFilter(TimelineKey: "t1", SpoilerSeriesMax: 1);
            Assert.Empty(await firstClaims.SearchApprovedAsync("east gate north road", filter, 10, ct));
            var autoApproved = Assert.Single((await firstClaims.ListAsync(RagClaimService.Approved, 1, ct)).Claims);
            Assert.Equal("auto:source-policy", autoApproved.ReviewedBy);
            var claim = Assert.Single((await firstClaims.ListAsync(RagClaimService.Pending, 1, ct)).Claims);
            Assert.Equal(gate.Title, claim.DocumentTitle);
            Assert.Equal("East Gate closes at sundown.", claim.Evidence);
            Assert.True(await firstClaims.SetPolicyAsync(imports[0].SourceId, 0.95m, ct));
            Assert.Single((await firstClaims.ListAsync(RagClaimService.Pending, 1, ct)).Claims);
            Assert.Equal(1, await firstClaims.ReviewAsync(claim.Id,
                new(RagClaimService.Pending, RagClaimService.Approved), "reviewer", ct));
            Assert.Equal(0, await secondClaims.ReviewAsync(claim.Id,
                new(RagClaimService.Pending, RagClaimService.Rejected), "reviewer", ct));
            Assert.Equal(["East Gate closes at sundown."],
                await firstClaims.SearchApprovedAsync("east gate north road", filter, 10, ct));
            Assert.Empty(await firstClaims.SearchApprovedAsync("east gate", filter with { TimelineKey = "other" }, 10, ct));
            Assert.Equal(1, await firstClaims.ReviewAsync(claim.Id,
                new(RagClaimService.Approved, RagClaimService.Rejected), "reviewer", ct));
            Assert.True(await firstClaims.SetPolicyAsync(imports[0].SourceId, 0.8m, ct));
            Assert.Empty(await firstClaims.SearchApprovedAsync("east gate", filter, 10, ct));

            var embeddings = new ParallelEmbeddingProvider();
            var configuration = new EmbeddingOptions { Enabled = true, ApiKey = "fake", BatchSize = 1 };
            var firstEmbedding = new RagEmbeddingService(first, embeddings, configuration, NullLogger<RagEmbeddingService>.Instance);
            var secondEmbedding = new RagEmbeddingService(second, embeddings, configuration, NullLogger<RagEmbeddingService>.Instance);
            var embedded = await Task.WhenAll(firstEmbedding.EmbedPendingAsync(ct), secondEmbedding.EmbedPendingAsync(ct));
            Assert.Equal([1, 1], embedded);
            Assert.Equal(2, embeddings.Passages.Distinct().Count());
            Assert.Equal(0, await firstEmbedding.EmbedPendingAsync(ct));
            Assert.Equal(2, (await import.StatusAsync(ct)).EmbeddedChunkCount);
        }
        finally
        {
            await first.Database.EnsureDeletedAsync(CancellationToken.None);
        }
    }

    private static RagImportRequest Request(string key, string text, int spoiler) =>
        new("canon.txt", "text/plain", Encoding.UTF8.GetBytes(text), key, "WorldBible", key,
            "Authorized", "Approved", "SharedApprovedCanon", "t1", spoiler);

    private sealed class ParallelExtractor : IClaimExtractor
    {
        private readonly TaskCompletionSource _overlap = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ConcurrentBag<string> Passages { get; } = [];

        public async Task<ExtractedClaim[]> ExtractAsync(string passage, CancellationToken ct)
        {
            Passages.Add(passage);
            if (Passages.Count == 2)
                _overlap.TrySetResult();
            await _overlap.Task.WaitAsync(ct);
            return passage.StartsWith("East Gate", StringComparison.Ordinal)
                ? [new("place", "East Gate", "closes at", "sundown.", 0.9m, passage)]
                : [new("place", "North Road", "leads to", "the ridge.", 0.95m, passage)];
        }
    }

    private sealed class ParallelEmbeddingProvider : IEmbeddingProvider
    {
        private readonly TaskCompletionSource _overlap = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ConcurrentBag<string> Passages { get; } = [];
        public string Model => "fake";
        public int Dimensions => 1024;

        public async Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> inputs, Guid? accountId,
            Guid? survivorId, CancellationToken ct)
        {
            Passages.Add(Assert.Single(inputs));
            if (Passages.Count == 2)
                _overlap.TrySetResult();
            await _overlap.Task.WaitAsync(ct);
            var vector = new float[Dimensions];
            vector[0] = 1;
            return [vector];
        }
    }
}
