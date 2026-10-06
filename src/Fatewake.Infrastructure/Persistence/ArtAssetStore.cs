using Microsoft.EntityFrameworkCore;

namespace Fatewake.Infrastructure.Persistence;

public sealed record ArtAssetMatch(Guid Id,string Key,int Version,string MasterPngStorageKey,string? WebPStorageKey,string? OptimizedPngStorageKey);

public interface IArtAssetStore
{
    Task<ArtAssetMatch?> FindApprovedByFingerprintAsync(string visualFingerprint,CancellationToken ct=default);
    Task<ArtAssetMatch?> FindApprovedAsync(string key,int version,CancellationToken ct=default);
}

public sealed class ArtAssetStore(FatewakeDbContext db):IArtAssetStore
{
    public Task<ArtAssetMatch?> FindApprovedByFingerprintAsync(string visualFingerprint,CancellationToken ct=default)
        => Query().Where(x=>x.Asset.VisualFingerprint==visualFingerprint).OrderByDescending(x=>x.Asset.Version)
            .Select(x=>new ArtAssetMatch(x.Asset.Id,x.Asset.Key,x.Asset.Version,x.Asset.MasterPngStorageKey,x.WebP,x.Png)).FirstOrDefaultAsync(ct);

    public Task<ArtAssetMatch?> FindApprovedAsync(string key,int version,CancellationToken ct=default)
        => Query().Where(x=>x.Asset.Key==key&&x.Asset.Version==version)
            .Select(x=>new ArtAssetMatch(x.Asset.Id,x.Asset.Key,x.Asset.Version,x.Asset.MasterPngStorageKey,x.WebP,x.Png)).FirstOrDefaultAsync(ct);

    private IQueryable<AssetProjection> Query()
        => db.ArtAssets.Where(a=>a.Status==ArtAssetStatus.Approved)
            .Select(a=>new AssetProjection(a,
                db.ArtAssetDerivatives.Where(d=>d.ArtAssetId==a.Id&&d.Kind==ArtDerivativeKind.WebP).Select(d=>d.StorageKey).FirstOrDefault(),
                db.ArtAssetDerivatives.Where(d=>d.ArtAssetId==a.Id&&d.Kind==ArtDerivativeKind.OptimizedPng).Select(d=>d.StorageKey).FirstOrDefault()));

    private sealed record AssetProjection(ArtAssetRecord Asset,string? WebP,string? Png);
}
