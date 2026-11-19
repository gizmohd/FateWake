namespace Fatewake.Web.Presentation;

/// <summary>Web view of a saved or previewed prompt revision and its optional durable regeneration job.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Web/Presentation/ArtPromptReviewSaveResult.md">ArtPromptReviewSaveResult documentation</see>.</remarks>
public sealed record ArtPromptReviewSaveResult(Guid RevisionId, int RevisionNumber, string PreviewResolvedPrompt,
    string PreviewNegativePrompt, bool Saved, bool RegenerationQueued, Guid? WorkJobId);
