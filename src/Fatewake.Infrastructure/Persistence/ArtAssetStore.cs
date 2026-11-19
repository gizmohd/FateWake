using Microsoft.EntityFrameworkCore;

namespace Fatewake.Infrastructure.Persistence;

/// <summary>Resolves approved artwork and delivery assets through translatable PostgreSQL queries.</summary>
/// <see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/ArtAssetStore.md">ArtAssetStore documentation</see>
public sealed class ArtAssetStore(FatewakeDbContext db) : IArtAssetStore
{
    /// <inheritdoc />
    public Task<ArtAssetMatch?> FindApprovedByFingerprintAsync(string visualFingerprint, CancellationToken ct = default, Guid? accountId = null)
        => Project(db.ArtAssets.AsNoTracking()
            .Where(a => a.Status == ArtAssetStatus.Approved && a.VisualFingerprint == visualFingerprint &&
                (a.OwnerAccountId == null || a.OwnerAccountId == accountId))
            .OrderBy(a => a.OwnerAccountId == accountId ? 0 : 1)
            .ThenByDescending(a => a.Version)).FirstOrDefaultAsync(ct);

    /// <inheritdoc />
    public Task<ArtAssetMatch?> FindApprovedAsync(string key, int version, CancellationToken ct = default, Guid? accountId = null)
        => Project(db.ArtAssets.AsNoTracking()
            .Where(a => a.Status == ArtAssetStatus.Approved && a.Key == key && a.Version == version &&
                (a.OwnerAccountId == null || a.OwnerAccountId == accountId)))
            .FirstOrDefaultAsync(ct);

    private IQueryable<ArtAssetMatch> Project(IQueryable<ArtAssetRecord> assets)
        => assets.Select(a => new ArtAssetMatch(a.Id, a.Key, a.Version, a.MasterPngStorageKey,
            db.ArtAssetDerivatives.Where(d => d.ArtAssetId == a.Id && d.Kind == ArtDerivativeKind.WebP)
                .Select(d => d.StorageKey).FirstOrDefault(),
            db.ArtAssetDerivatives.Where(d => d.ArtAssetId == a.Id && d.Kind == ArtDerivativeKind.OptimizedPng)
                .Select(d => d.StorageKey).FirstOrDefault()));
}
