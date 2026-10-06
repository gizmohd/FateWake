namespace Fatewake.Infrastructure.Art;
/// <summary>Describes canonical visual state and complete provider-facing provenance needed to create or reuse artwork asynchronously.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Art/ArtWorkRequest.md">ArtWorkRequest documentation</see>.</remarks>
public sealed record ArtWorkRequest(
 string AssetKey,string AssetType,string VisualFingerprint,string ResolvedPrompt,string Provider,string Model,
 string? CharacterKey=null,string? LocationKey=null,int Priority=0,
 string? NegativeInstructions=null,string? PromptTemplateId=null,string? PromptTemplateVersion=null,string? StyleBibleVersion=null,
 IReadOnlyList<ArtReferenceImage>? ReferenceImages=null);
