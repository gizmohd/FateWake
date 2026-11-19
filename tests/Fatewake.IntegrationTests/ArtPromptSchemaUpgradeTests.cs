using Fatewake.Api;
using Fatewake.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace Fatewake.IntegrationTests;

/// <summary>Verifies startup can repair installations whose applied prompt migration lacked the visual context column.</summary>
public sealed class ArtPromptSchemaUpgradeTests
{
    /// <summary>Repairs the old schema and lets the importer run without replacing existing prompts.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Repairs_older_prompt_schema_before_startup_import(bool hasExistingPrompts)
    {
        var connection = Environment.GetEnvironmentVariable("FATEWAKE_ART_TEST_CONNECTION");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(connection), "An isolated PostgreSQL server is required.");
        var database = new NpgsqlConnectionStringBuilder(connection)
        {
            Database = "fatewake_art_schema_" + Guid.NewGuid().ToString("N")
        };
        var ct = TestContext.Current.CancellationToken;
        await using var db = new FatewakeDbContext(new DbContextOptionsBuilder<FatewakeDbContext>()
            .UseNpgsql(database.ConnectionString).Options);
        var importer = new ArtPromptSeedImporter(db, new SceneArtCatalog(new ConfigurationBuilder().Build()),
            NullLogger<ArtPromptSeedImporter>.Instance);

        try
        {
            await db.GetService<IMigrator>().MigrateAsync("20261119110000_SqlArtPrompts", ct);
            ArtPromptRecord? existing = null;
            if (hasExistingPrompts)
            {
                await importer.InitializeAsync(ct);
                existing = await db.Set<ArtPromptRecord>().AsNoTracking().FirstAsync(ct);
            }
            await db.Database.ExecuteSqlRawAsync("""ALTER TABLE art_prompt DROP COLUMN "ReachedVisualContext";""", ct);
            db.ChangeTracker.Clear();

            await DatabaseInitializer.InitializeAsync(db, ct);
            await importer.InitializeAsync(ct);
            var prompts = db.Set<ArtPromptRecord>().AsNoTracking();
            var repaired = existing is null
                ? await prompts.FirstAsync(ct)
                : await prompts.SingleAsync(row => row.Id == existing.Id, ct);
            Assert.NotNull(repaired.CurrentRevisionId);
            if (existing is not null)
                Assert.Equal(existing.CurrentRevisionId, repaired.CurrentRevisionId);
            Assert.Null(repaired.ReachedVisualContext);
        }
        finally
        {
            await db.Database.EnsureDeletedAsync(CancellationToken.None);
        }
    }
}
