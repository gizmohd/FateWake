namespace Fatewake.Infrastructure.Art;
/// <summary>Identifies an approved durable image that may guide provider generation or editing.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Art/ArtReferenceImage.md">ArtReferenceImage documentation</see>.</remarks>
public sealed record ArtReferenceImage(string StorageKey,string Role);