namespace Fatewake.Api;

/// <summary>Describes a saved or previewed prompt revision and its optional durable regeneration job.</summary>
/// <see href="../../../docs/code/src/Fatewake.Api/ArtPromptReviewSaveResult.md">Documentation</see>
public sealed record ArtPromptReviewSaveResult(Guid RevisionId, int RevisionNumber, string PreviewResolvedPrompt,
    string PreviewNegativePrompt, bool Saved, bool RegenerationQueued, Guid? WorkJobId);
