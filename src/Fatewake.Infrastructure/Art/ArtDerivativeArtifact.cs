namespace Fatewake.Infrastructure.Art;
/// <summary>Describes a durable encoded image derivative produced by an artwork job.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Art/ArtDerivativeArtifact.md">ArtDerivativeArtifact documentation</see>.</remarks>
public sealed record ArtDerivativeArtifact(string StorageKey,string ContentType,string ContentHash,int Width,int Height,long ByteSize,string EncoderMetadata);