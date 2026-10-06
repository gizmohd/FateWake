namespace Fatewake.Infrastructure.Art;
/// <summary>Describes decoded image dimensions and alpha-channel presence.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Art/ArtImageInfo.md">ArtImageInfo documentation</see>.</remarks>
public sealed record ArtImageInfo(int Width,int Height,bool HasAlpha);