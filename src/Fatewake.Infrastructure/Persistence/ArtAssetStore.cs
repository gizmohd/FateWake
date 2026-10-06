using Microsoft.EntityFrameworkCore;

namespace Fatewake.Infrastructure.Persistence;

/// <summary>Resolves approved artwork and delivery assets through translatable PostgreSQL queries.</summary>
/// <see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/ArtAssetStore.md">ArtAssetStore documentation</see>
public sealed class ArtAssetStore(FatewakeDbContext db) : IArtAssetStore
{
    /// <inheritdoc />
    public Task<ArtAssetMatch?> FindApprovedByFingerprintAsync(string visualFingerprint, CancellationToken ct = default)
        => Project(db.ArtAssets.AsNoTracking()
            .Where(a => a.Status == ArtAssetStatus.Approved && a.VisualFingerprint == visualFingerprint)
            .OrderByDescending(a => a.Version)).FirstOrDefaultAsync(ct);

    /// <inheritdoc />
    public Task<ArtAssetMatch?> FindApprovedAsync(string key, int version, CancellationToken ct = default)
        => Project(db.ArtAssets.AsNoTracking()
            .Where(a => a.Status == ArtAssetStatus.Approved && a.Key == key && a.Version == version))
            .FirstOrDefaultAsync(ct);

    private IQueryable<ArtAssetMatch> Project(IQueryable<ArtAssetRecord> assets)
        => assets.Select(a => new ArtAssetMatch(a.Id, a.Key, a.Version, a.MasterPngStorageKey,
            db.ArtAssetDerivatives.Where(d => d.ArtAssetId == a.Id && d.Kind == ArtDerivativeKind.WebP)
                .Select(d => d.StorageKey).FirstOrDefault(),
            db.ArtAssetDerivatives.Where(d => d.ArtAssetId == a.Id && d.Kind == ArtDerivativeKind.OptimizedPng)
                .Select(d => d.StorageKey).FirstOrDefault()));
}
