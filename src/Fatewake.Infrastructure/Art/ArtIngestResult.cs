namespace Fatewake.Infrastructure.Art;

/// <summary>Identifies the registered master and its web-delivery derivatives.</summary>
/// <param name="AssetId">Registered immutable asset identifier.</param>
/// <param name="Key">Logical artwork key.</param>
/// <param name="Version">Immutable asset version.</param>
/// <param name="MasterPngStorageKey">Archival PNG storage key.</param>
/// <param name="WebPStorageKey">WebP delivery storage key.</param>
/// <param name="OptimizedPngStorageKey">Optimized PNG fallback storage key.</param>
/// <see href="../../../docs/code/src/Fatewake.Infrastructure/Art/ArtIngestResult.md">ArtIngestResult documentation</see>
public sealed record ArtIngestResult(
    Guid AssetId,
    string Key,
    int Version,
    string MasterPngStorageKey,
    string WebPStorageKey,
    string OptimizedPngStorageKey);
