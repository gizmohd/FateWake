namespace Fatewake.Infrastructure.Art;
/// <summary>Contains an encoded delivery image and deterministic encoder metadata.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Art/EncodedArtImage.md">EncodedArtImage documentation</see>.</remarks>
public sealed record EncodedArtImage(byte[] Bytes,string ContentType,string EncoderMetadata,ArtImageInfo Info);