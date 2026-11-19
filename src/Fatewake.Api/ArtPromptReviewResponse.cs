namespace Fatewake.Api;

/// <summary>Returns exact source-generation provenance and the current prompt-edit/replacement state to administrators.</summary>
/// <see href="../../../docs/code/src/Fatewake.Api/ArtPromptReviewResponse.md">Documentation</see>
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
