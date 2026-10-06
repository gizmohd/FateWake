namespace Fatewake.Infrastructure.Art;
/// <summary>Describes inspected master-image properties and content identity.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Art/ArtMetadataArtifact.md">ArtMetadataArtifact documentation</see>.</remarks>
public sealed record ArtMetadataArtifact(string ContentHash,int Width,int Height,bool HasAlpha,long ByteSize);