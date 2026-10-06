namespace Fatewake.Infrastructure.Art;

public sealed record ArtImageInfo(int Width,int Height,bool HasAlpha);
public sealed record EncodedArtImage(byte[] Bytes,string ContentType,string EncoderMetadata,ArtImageInfo Info);

public interface IArtImageProcessor
{
    ArtImageInfo InspectPng(ReadOnlyMemory<byte> png);
    Task<EncodedArtImage> CreateWebPAsync(ReadOnlyMemory<byte> masterPng,CancellationToken ct=default);
    Task<EncodedArtImage> CreateOptimizedPngAsync(ReadOnlyMemory<byte> masterPng,CancellationToken ct=default);
}

// Encoding is intentionally provider-neutral. A production implementation can use an image library
// or external image service without coupling AI generation to storage/persistence.
