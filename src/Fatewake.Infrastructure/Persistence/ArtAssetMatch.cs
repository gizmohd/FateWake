namespace Fatewake.Infrastructure.Persistence;

/// <summary>Contains the immutable approved artwork identity and its reusable binary storage keys.</summary>
/// <param name="Id">Approved asset identifier.</param>
/// <param name="Key">Logical asset key.</param>
/// <param name="Version">Immutable asset version.</param>
/// <param name="MasterPngStorageKey">Canonical master storage key.</param>
/// <param name="WebPStorageKey">Optional WebP delivery key.</param>
/// <param name="OptimizedPngStorageKey">Optional optimized PNG delivery key.</param>
/// <see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/ArtAssetMatch.md">ArtAssetMatch documentation</see>
public sealed record ArtAssetMatch(Guid Id, string Key, int Version, string MasterPngStorageKey, string? WebPStorageKey, string? OptimizedPngStorageKey);
