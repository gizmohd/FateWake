using System.Security.Cryptography;
using Fatewake.Infrastructure.Art;
using Fatewake.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fatewake.IntegrationTests;

/// <summary>Verifies PostgreSQL binary storage and restartable legacy-asset backfill behavior.</summary>
/// <see href="../../../docs/code/tests/Fatewake.IntegrationTests/ArtBinaryStorageTests.md">ArtBinaryStorageTests documentation</see>
public sealed class ArtBinaryStorageTests
{
    /// <summary>Writes replace a logical binary key and reads return the latest exact bytes.</summary>
    [Fact]
    public async Task Postgres_storage_persists_and_replaces_binary_bytes()
    {
        await using var db = CreateDatabase();
        var storage = CreateStorage(db);
        var key = "test/master.png";
        await storage.PutAsync(key, new MemoryStream([1, 2, 3]), "image/png", TestContext.Current.CancellationToken);
        await storage.PutAsync(key, new MemoryStream([4, 5]), "image/png", TestContext.Current.CancellationToken);

        Assert.True(await storage.ExistsAsync(key, TestContext.Current.CancellationToken));
        await using var read = await storage.OpenReadAsync(key, TestContext.Current.CancellationToken);
        using var buffer = new MemoryStream();
        await read.CopyToAsync(buffer, TestContext.Current.CancellationToken);
        Assert.Equal(new byte[] { 4, 5 }, buffer.ToArray());
        var record = await db.ArtBinaries.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, record.ByteSize);
        Assert.Equal(Hash([4, 5]), record.ContentHash);
    }

    /// <summary>Backfill verifies masters and derivatives, is idempotent, and preserves the filesystem source.</summary>
    [Fact]
    public async Task Backfill_imports_registered_assets_once_and_keeps_legacy_source()
    {
        await using var db = CreateDatabase();
        var root = Path.Combine(Path.GetTempPath(), "fatewake-art-backfill-" + Guid.NewGuid().ToString("N"));
        var configuredRoot = Path.Combine(root, "data", "art");
        var apiRoot = Path.Combine(root, "src", "Fatewake.Api");
        var legacyRoot = Path.Combine(root, "src", "Fatewake.Web", "wwwroot", "art");
        Directory.CreateDirectory(apiRoot);
        try
        {
            var legacy = new FileSystemArtBinaryStorage(legacyRoot);
            var master = new byte[] { 1, 2, 3, 4 };
            var derivative = new byte[] { 5, 6, 7 };
            await legacy.PutAsync("scene/master.png", new MemoryStream(master), "image/png", TestContext.Current.CancellationToken);
            await legacy.PutAsync("scene/delivery.webp", new MemoryStream(derivative), "image/webp", TestContext.Current.CancellationToken);
            var now = DateTimeOffset.UtcNow;
            var assetId = Guid.NewGuid();
            db.ArtAssets.Add(new ArtAssetRecord
            {
                Id = assetId,
                Key = "scene",
                Version = 1,
                AssetType = "Background",
                Status = ArtAssetStatus.Approved,
                VisualFingerprint = "scene-fingerprint",
                ContentHash = Hash(master),
                MasterPngStorageKey = "scene/master.png",
                Width = 1,
                Height = 1,
                HasAlpha = false,
                CreatedAt = now,
                ApprovedAt = now
            });
            db.ArtAssetDerivatives.Add(new ArtAssetDerivativeRecord
            {
                Id = Guid.NewGuid(),
                ArtAssetId = assetId,
                Kind = ArtDerivativeKind.WebP,
                StorageKey = "scene/delivery.webp",
                ContentType = "image/webp",
                ContentHash = Hash(derivative),
                Width = 1,
                Height = 1,
                ByteSize = derivative.Length,
                CreatedAt = now
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var storage = CreateStorage(db, configuredRoot);
            var environment = new HostApplicationBuilder().Environment;
            environment.ContentRootPath = apiRoot;
            var backfill = new ArtBinaryBackfillService(db, storage, Options.Create(new ArtStorageOptions { RootPath = configuredRoot }), environment,
                NullLogger<ArtBinaryBackfillService>.Instance);
            Assert.Equal(2, await backfill.BackfillAsync(TestContext.Current.CancellationToken));
            Assert.Equal(0, await backfill.BackfillAsync(TestContext.Current.CancellationToken));
            Assert.Equal(master, await File.ReadAllBytesAsync(Path.Combine(legacyRoot, "scene", "master.png"), TestContext.Current.CancellationToken));
            Assert.Equal(2, await db.ArtBinaries.CountAsync(TestContext.Current.CancellationToken));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>Missing legacy bytes do not block API startup; scene resolution can queue replacement artwork.</summary>
    [Fact]
    public async Task Backfill_skips_missing_registered_binaries_without_throwing()
    {
        await using var db = CreateDatabase();
        var root = Path.Combine(Path.GetTempPath(), "fatewake-art-missing-" + Guid.NewGuid().ToString("N"));
        var apiRoot = Path.Combine(root, "src", "Fatewake.Api");
        Directory.CreateDirectory(apiRoot);
        try
        {
            var now = DateTimeOffset.UtcNow;
            db.ArtAssets.Add(new ArtAssetRecord
            {
                Id = Guid.NewGuid(),
                Key = "scene",
                Version = 1,
                AssetType = "Background",
                Status = ArtAssetStatus.Approved,
                VisualFingerprint = "scene-fingerprint",
                ContentHash = Hash([1, 2, 3]),
                MasterPngStorageKey = "scene/master.png",
                Width = 1,
                Height = 1,
                HasAlpha = false,
                CreatedAt = now,
                ApprovedAt = now
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var rootPath = Path.Combine(root, "data", "art");
            var storage = CreateStorage(db, rootPath);
            var environment = new HostApplicationBuilder().Environment;
            environment.ContentRootPath = apiRoot;
            var backfill = new ArtBinaryBackfillService(db, storage,
                Options.Create(new ArtStorageOptions { RootPath = rootPath }), environment,
                NullLogger<ArtBinaryBackfillService>.Instance);

            Assert.Equal(0, await backfill.BackfillAsync(TestContext.Current.CancellationToken));
            Assert.Empty(await db.ArtBinaries.ToListAsync(TestContext.Current.CancellationToken));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static FatewakeDbContext CreateDatabase()
        => new(new DbContextOptionsBuilder<FatewakeDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);

    private static PostgresArtBinaryStorage CreateStorage(FatewakeDbContext db, string? root = null)
        => new(db, Options.Create(new ArtStorageOptions { RootPath = root }), NullLogger<PostgresArtBinaryStorage>.Instance);

    private static string Hash(byte[] bytes)
        => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
