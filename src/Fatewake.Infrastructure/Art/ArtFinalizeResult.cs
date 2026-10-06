namespace Fatewake.Infrastructure.Art;
/// <summary>Identifies the approved asset selected by finalization and how many waiting survivor identities were activated.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Art/ArtFinalizeResult.md">ArtFinalizeResult documentation</see>.</remarks>
public sealed record ArtFinalizeResult(Guid AssetId,bool Reused,int ActivatedSurvivors);