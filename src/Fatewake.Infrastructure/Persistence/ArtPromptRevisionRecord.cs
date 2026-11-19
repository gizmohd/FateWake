namespace Fatewake.Infrastructure.Persistence;

/// <summary>Stores an immutable, versioned positive and negative artwork prompt revision.</summary>
/// <remarks><see href="../../../../docs/code/src/Fatewake.Infrastructure/Persistence/ArtPromptRevisionRecord.md">Documentation</see>.</remarks>
public sealed class ArtPromptRevisionRecord
{
    /// <summary>Stable immutable revision identifier.</summary>
    public Guid Id
    {
        get; set;
    }
    /// <summary>Owning prompt identity.</summary>
    public Guid ArtPromptId
    {
        get; set;
    }
    /// <summary>Monotonically allocated revision number within the identity.</summary>
    public int RevisionNumber
    {
        get; set;
    }
    /// <summary>Previous immutable revision when this revision was derived from one.</summary>
    public Guid? ParentRevisionId
    {
        get; set;
    }
    /// <summary>Validated subject-only positive prompt text.</summary>
    public required string PositivePrompt
    {
        get; set;
    }
    /// <summary>Optional additional negative prompt text.</summary>
    public string? NegativePrompt
    {
        get; set;
    }
    /// <summary>Revision origin: seed, ai or admin.</summary>
    public required string Source
    {
        get; set;
    }
    /// <summary>Administrator account that authored this revision, if applicable.</summary>
    public Guid? CreatorAccountId
    {
        get; set;
    }
    /// <summary>Text model that authored this revision, if applicable.</summary>
    public string? Model
    {
        get; set;
    }
    /// <summary>Captured canonical authoring context fingerprint.</summary>
    public string? RequestContextFingerprint
    {
        get; set;
    }
    /// <summary>Immutable creation time.</summary>
    public DateTimeOffset CreatedAt
    {
        get; set;
    }
}
