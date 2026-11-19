namespace Fatewake.Infrastructure.Persistence;

/// <summary>Identifies one SQL-backed prompt family for a canonical scene layer and visual context.</summary>
/// <remarks><see href="../../../../docs/code/src/Fatewake.Infrastructure/Persistence/ArtPromptRecord.md">Documentation</see>.</remarks>
public sealed class ArtPromptRecord
{
    /// <summary>Stable identity for the prompt row.</summary>
    public Guid Id
    {
        get; set;
    }
    /// <summary>Server-derived key including layer, canonical context, style and privacy scope.</summary>
    public required string IdentityKey
    {
        get; set;
    }
    /// <summary>Allowlisted scene-layer key.</summary>
    public required string AssetKey
    {
        get; set;
    }
    /// <summary>Fingerprint of canonical visual context, excluding incidental branch identifiers.</summary>
    public required string ContextFingerprint
    {
        get; set;
    }
    /// <summary>Allowlisted reached outcome required to reconstruct an outcome-dependent prompt identity.</summary>
    public string? ReachedVisualContext
    {
        get; set;
    }
    /// <summary>Version of the shared runtime style contract used for applicability.</summary>
    public required string StyleBibleVersion
    {
        get; set;
    }
    /// <summary>Owning account for personalized/private prompt context; null for shared prompts.</summary>
    public Guid? OwnerAccountId
    {
        get; set;
    }
    /// <summary>Whether this prompt is restricted to its canonical owner.</summary>
    public bool IsPrivate
    {
        get; set;
    }
    /// <summary>Readiness state: pending, ready, disabled or failed.</summary>
    public string Status { get; set; } = "pending";
    /// <summary>Most recently allocated immutable revision number.</summary>
    public int? LatestRevisionNumber
    {
        get; set;
    }
    /// <summary>Most recently allocated immutable revision, including save-only drafts.</summary>
    public Guid? LatestRevisionId
    {
        get; set;
    }
    /// <summary>Revision currently bound to ordinary scene requests.</summary>
    public int? CurrentRevisionNumber
    {
        get; set;
    }
    /// <summary>Current serving revision, promoted only after a requested replacement succeeds.</summary>
    public Guid? CurrentRevisionId
    {
        get; set;
    }
    /// <summary>Latest administrator-requested revision awaiting selected replacement work.</summary>
    public int? RequestedRevisionNumber
    {
        get; set;
    }
    /// <summary>Requested replacement revision; older delayed jobs cannot promote over a newer value.</summary>
    public Guid? RequestedRevisionId
    {
        get; set;
    }
    /// <summary>Exact approved source asset selected for replacement.</summary>
    public Guid? RequestedAssetId
    {
        get; set;
    }
    /// <summary>Durable image job intended to publish the requested revision.</summary>
    public Guid? RequestedWorkJobId
    {
        get; set;
    }
    /// <summary>Durable authoring job coalesced for a missing prompt.</summary>
    public Guid? AuthoringJobId
    {
        get; set;
    }
    /// <summary>Bounded machine-readable terminal failure code.</summary>
    public string? FailureCode
    {
        get; set;
    }
    /// <summary>Prompt identity creation time.</summary>
    public DateTimeOffset CreatedAt
    {
        get; set;
    }
    /// <summary>Last state or binding change time.</summary>
    public DateTimeOffset UpdatedAt
    {
        get; set;
    }
}
