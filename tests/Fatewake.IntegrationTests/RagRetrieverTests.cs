using Fatewake.Infrastructure.Persistence;
using Fatewake.Infrastructure.Rag;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fatewake.IntegrationTests;

/// <summary>Verifies retrieval authorization, spoiler and timeline filtering against PostgreSQL.</summary>
public sealed class RagRetrieverTests
{
    /// <summary>Returns shared canon, hides other survivors' and timelines' private story, and enforces spoiler limits.</summary>
    [Fact]
    public async Task Filters_private_story_timeline_and_spoilers()
    {
        var connection = Environment.GetEnvironmentVariable("FATEWAKE_TEST_CONNECTION");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(connection), "Requires an isolated FATEWAKE_TEST_CONNECTION database.");
        var database = new Npgsql.NpgsqlConnectionStringBuilder(connection)
        {
            Database = "fatewake_rag_" + Guid.NewGuid().ToString("N")
        };
        var options = new DbContextOptionsBuilder<FatewakeDbContext>().UseNpgsql(database.ConnectionString).Options;
        await using var db = new FatewakeDbContext(options);
        var ct = TestContext.Current.CancellationToken;
        var mine = Guid.NewGuid();
        try
        {
            await db.Database.MigrateAsync(ct);
            Add(db, "canon", "SharedApprovedCanon", "{}", null, null, "lantern beacon on the ridge");
            Add(db, "spoiler", "SharedApprovedCanon", "{}", null, 3, "lantern beacon secret ending");
            Add(db, "mine", "Private", $$"""{"survivorId":"{{mine}}"}""", "t1", null, "lantern beacon I carried");
            Add(db, "other-timeline", "Private", $$"""{"survivorId":"{{mine}}"}""", "t2", null, "lantern beacon elsewhere");
            Add(db, "other-survivor", "Private", $$"""{"survivorId":"{{Guid.NewGuid()}}"}""", "t1", null, "lantern beacon stolen");
            Add(db, "generated-thread", "Private", $$"""{"survivorId":"{{mine}}"}""", "t1", null,
                "lantern beacon from a generated scene", "GeneratedThreadCanon");
            await db.SaveChangesAsync(ct);
            var retriever = new RagRetriever(db, null!, new EmbeddingOptions(), NullLogger<RagRetriever>.Instance);

            var result = await retriever.SearchAsync("lantern beacon", new RagSearchFilter(mine, "t1", 1), 10, ct);

            Assert.Equal(["canon", "mine"], result.Select(x => x.SourceKey).Order().ToArray());
        }
        finally
        {
            await db.Database.EnsureDeletedAsync(CancellationToken.None);
        }
    }

    private static void Add(FatewakeDbContext db, string key, string visibility, string metadata, string? timeline,
        int? spoilerSeries, string text, string sourceType = "StoryLine")
    {
        var now = DateTimeOffset.UtcNow;
        var source = new RagSourceRecord
        {
            Id = Guid.NewGuid(),
            SourceKey = key,
            SourceType = sourceType,
            Title = key,
            Origin = "test",
            ContentHash = key,
            LicenseStatus = "Authorized",
            ReviewStatus = "Approved",
            Visibility = visibility,
            Metadata = metadata,
            CreatedAt = now,
            UpdatedAt = now
        };
        var document = new RagDocumentRecord
        {
            Id = Guid.NewGuid(),
            SourceId = source.Id,
            DocumentKey = key,
            DocumentType = "txt",
            Title = key,
            ContentHash = key,
            Status = "Approved",
            CreatedAt = now,
            UpdatedAt = now
        };
        db.AddRange(source, document, new RagChunkRecord
        {
            Id = Guid.NewGuid(),
            DocumentId = document.Id,
            ChunkKey = key,
            Ordinal = 0,
            Text = text,
            TimelineKey = timeline,
            SpoilerSeriesMin = spoilerSeries,
            CanonStatus = "Candidate",
            ReviewStatus = "Approved",
            CreatedAt = now,
            UpdatedAt = now
        });
    }
}
