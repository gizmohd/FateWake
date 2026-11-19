namespace Fatewake.Infrastructure.Art;

/// <summary>Persists generated or edited artwork through the canonical asset pipeline.</summary>
/// <see href="../../../docs/code/src/Fatewake.Infrastructure/Art/IArtAssetIngestionService.md">IArtAssetIngestionService documentation</see>
public interface IArtAssetIngestionService
{
    /// <summary>Registers an archival master and delivery derivatives without replacing prior asset versions.</summary>
    /// <param name="request">Canonical asset content and provenance.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The immutable registered asset and delivery storage keys.</returns>
    Task<ArtIngestResult> IngestAsync(ArtIngestRequest request, CancellationToken ct = default);
}
