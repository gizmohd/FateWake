namespace Fatewake.Infrastructure.Art;
/// <summary>Inspects canonical PNG masters and creates deterministic delivery derivatives.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Art/IArtImageProcessor.md">IArtImageProcessor documentation</see>.</remarks>
public interface IArtImageProcessor
{
    ArtImageInfo Inspect(ReadOnlyMemory<byte> image);
    ArtImageInfo InspectPng(ReadOnlyMemory<byte> png);
    Task<EncodedArtImage> CreateWebPAsync(ReadOnlyMemory<byte> masterPng,CancellationToken ct=default);
    Task<EncodedArtImage> CreateOptimizedPngAsync(ReadOnlyMemory<byte> masterPng,CancellationToken ct=default);
}