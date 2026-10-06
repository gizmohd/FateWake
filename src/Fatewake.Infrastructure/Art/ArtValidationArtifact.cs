namespace Fatewake.Infrastructure.Art;
/// <summary>Records deterministic validation of a master image and its required delivery derivatives.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Art/ArtValidationArtifact.md">ArtValidationArtifact documentation</see>.</remarks>
public sealed record ArtValidationArtifact(bool Valid,string? Error);