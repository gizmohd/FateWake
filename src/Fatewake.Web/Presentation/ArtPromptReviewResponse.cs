namespace Fatewake.Web.Presentation;

/// <summary>Web view of exact source-generation provenance and the current prompt-edit/replacement state.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Web/Presentation/ArtPromptReviewResponse.md">ArtPromptReviewResponse documentation</see>.</remarks>
public sealed record ArtPromptReviewResponse(
    string AssetKey,
    int Version,
    bool OriginalSubmissionAvailable,
    string? OriginalResolvedPrompt,
    string? OriginalNegativePrompt,
    string? OriginalProvider,
    string? OriginalModel,
    string? OriginalProviderJobId,
    Guid? OriginalPromptRevisionId,
    string? OriginalPromptSource,
    DateTimeOffset? OriginalSubmittedAt,
    Guid? CurrentRevisionId,
    Guid? LatestRevisionId,
    int? LatestRevisionNumber,
    string? EditablePositivePrompt,
    string? EditableNegativePrompt,
    string EnforcedStyle,
    string EnforcedExclusions,
    string Framing,
    string? ReplacementStatus,
    Guid? ReplacementWorkJobId,
    int? ReplacementVersion);
