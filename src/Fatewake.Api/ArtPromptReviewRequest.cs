namespace Fatewake.Api;

/// <summary>Submits an administrator-authored immutable prompt revision against one exact asset version.</summary>
/// <param name="SceneKey">Stable scene identifier associated with the selected composition.</param>
/// <param name="AssetKey">Server-allowlisted layer key.</param>
/// <param name="Version">Exact approved source asset version selected by the administrator.</param>
/// <param name="ExpectedRevisionId">Latest prompt revision seen by the editor; stale values conflict.</param>
/// <param name="PositivePrompt">Editable subject-only positive prompt text.</param>
/// <param name="NegativePrompt">Optional additional negative prompt text.</param>
/// <param name="Regenerate">When true, explicitly queues a replacement for the selected source asset.</param>
/// <see href="../../../docs/code/src/Fatewake.Api/ArtPromptReviewRequest.md">Documentation</see>
public sealed record ArtPromptReviewRequest(string SceneKey, string AssetKey, int Version,
    Guid ExpectedRevisionId, string PositivePrompt, string? NegativePrompt, bool Regenerate);
