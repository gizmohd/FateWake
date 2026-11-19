namespace Fatewake.Web.Presentation;

/// <summary>Web request body for an administrator-authored prompt revision against one exact asset version.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Web/Presentation/ArtPromptReviewRequest.md">ArtPromptReviewRequest documentation</see>.</remarks>
public sealed record ArtPromptReviewRequest(string SceneKey, string AssetKey, int Version,
    Guid ExpectedRevisionId, string PositivePrompt, string? NegativePrompt, bool Regenerate);
