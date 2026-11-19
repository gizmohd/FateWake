using System.Security.Cryptography;
using Fatewake.Infrastructure.Persistence;
using Fatewake.Observability;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fatewake.Infrastructure.Art;

/// <summary>Imports registered legacy artwork into PostgreSQL and verifies each stored binary against its metadata hash.</summary>
/// <param name="db">Database containing approved asset and derivative references.</param>
/// <param name="storage">PostgreSQL-backed binary storage.</param>
/// <param name="options">Storage configuration containing the legacy filesystem root.</param>
/// <param name="environment">Host paths used to locate the previous Web static-art root.</param>
/// <param name="log">Actionable migration progress and failure logging.</param>
/// <see href="../../../docs/code/src/Fatewake.Infrastructure/Art/ArtBinaryBackfillService.md">ArtBinaryBackfillService documentation</see>
public sealed class ArtBinaryBackfillService(
    FatewakeDbContext db,
    IArtBinaryStorage storage,
    IOptions<ArtStorageOptions> options,
    IHostEnvironment environment,
    ILogger<ArtBinaryBackfillService> log)
{
    /// <summary>Copies missing registered masters and delivery derivatives without deleting filesystem sources.</summary>
    public async Task<int> BackfillAsync(CancellationToken ct = default)
    {
        using var operation = OperationTelemetry.Start("art.binary.backfill", log);
        try
        {
            return await BackfillCoreAsync(ct);
        }
        catch (OperationCanceledException ex) when (ct.IsCancellationRequested)
        {
            operation.Fail(ex);
            throw;
        }
        catch (Exception ex)
        {
            operation.Fail(ex);
            log.LogError(ex, "Registered artwork binary backfill failed");
            throw;
        }
    }

    private async Task<int> BackfillCoreAsync(CancellationToken ct)
    {
        var legacyRoots = new[]
            {
                options.Value.RootPath,
                Path.Combine(environment.ContentRootPath, "..", "Fatewake.Web", "wwwroot", "art")
            }
            .OfType<string>()
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(Path.GetFullPath)
            .Distinct(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
            .Select(x => new FileSystemArtBinaryStorage(x))
            .ToArray();
        var references = await db.ArtAssets.AsNoTracking()
            .Select(x => new { x.MasterPngStorageKey, x.ContentHash })
            .ToListAsync(ct);
        var derivativeReferences = await db.ArtAssetDerivatives.AsNoTracking()
            .Select(x => new { x.StorageKey, x.ContentHash })
            .ToListAsync(ct);
        var keys = references.Select(x => (Key: x.MasterPngStorageKey, Hash: x.ContentHash))
            .Concat(derivativeReferences.Select(x => (Key: x.StorageKey, Hash: x.ContentHash)))
            .GroupBy(x => x.Key, StringComparer.Ordinal)
            .Select(group =>
            {
                var hashes = group.Select(x => x.Hash).Distinct(StringComparer.Ordinal).ToArray();
                if (hashes.Length != 1)
                    throw new InvalidDataException($"Registered artwork key '{group.Key}' has conflicting content hashes.");
                return (Key: group.Key, Hash: hashes[0]);
            })
            .ToArray();
        if (keys.Length == 0)
            return 0;
        var imported = 0;
        var missing = new List<string>();
        foreach (var reference in keys)
        {
            ct.ThrowIfCancellationRequested();
            var existing = await db.ArtBinaries.AsNoTracking()
                .Where(x => x.StorageKey == reference.Key)
                .Select(x => x.Bytes)
                .SingleOrDefaultAsync(ct);
            if (existing is not null)
            {
                var existingHash = Convert.ToHexString(SHA256.HashData(existing)).ToLowerInvariant();
                if (!string.Equals(existingHash, reference.Hash, StringComparison.Ordinal))
                    throw new InvalidDataException($"PostgreSQL artwork hash does not match registered metadata for '{reference.Key}'.");
                continue;
            }

            FileSystemArtBinaryStorage? source = null;
            foreach (var legacySource in legacyRoots)
            {
                if (!await legacySource.ExistsAsync(reference.Key, ct))
                    continue;
                source = legacySource;
                break;
            }
            if (source is null)
            {
                missing.Add(reference.Key);
                continue;
            }
            await using var input = await source.OpenReadAsync(reference.Key, ct);
            using var buffer = new MemoryStream();
            await input.CopyToAsync(buffer, ct);
            var bytes = buffer.ToArray();
            var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            if (!string.Equals(hash, reference.Hash, StringComparison.Ordinal))
                throw new InvalidDataException($"Legacy artwork hash does not match registered metadata for '{reference.Key}'.");
            await using var content = new MemoryStream(bytes, writable: false);
            await storage.PutAsync(reference.Key, content, ContentTypeFor(reference.Key), ct);
            var stored = await db.ArtBinaries.AsNoTracking()
                .Where(x => x.StorageKey == reference.Key)
                .Select(x => new { x.ContentHash, x.Bytes })
                .SingleAsync(ct);
            var actualStoredHash = Convert.ToHexString(SHA256.HashData(stored.Bytes)).ToLowerInvariant();
            if (!string.Equals(stored.ContentHash, reference.Hash, StringComparison.Ordinal) ||
                !string.Equals(actualStoredHash, reference.Hash, StringComparison.Ordinal))
                throw new InvalidDataException($"Backfilled artwork failed verification for '{reference.Key}'.");
            imported++;
        }

        if (imported > 0)
            log.LogInformation("Backfilled {ArtworkBinaryCount} registered artwork binaries into PostgreSQL", imported);
        if (missing.Count > 0)
            log.LogWarning(
                "Could not backfill {MissingArtworkBinaryCount} registered artwork binaries; authored scene requests will queue regeneration. Missing keys: {MissingArtworkKeys}",
                missing.Count,
                string.Join(", ", missing.Take(10)));
        return imported;
    }

    private static string ContentTypeFor(string key)
        => Path.GetExtension(key).ToLowerInvariant() switch
        {
            ".webp" => "image/webp",
            ".png" => "image/png",
            _ => "application/octet-stream"
        };
}
