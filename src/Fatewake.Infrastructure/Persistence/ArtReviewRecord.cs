namespace Fatewake.Infrastructure.Persistence;

/// <summary>Durable administrator feedback against the exact scene or layer versions reviewed.</summary>
public sealed class ArtReviewRecord
{
    public Guid Id
    {
        get; set;
    }
    public Guid AccountId
    {
        get; set;
    }
    public required string SceneKey
    {
        get; set;
    }
    public string? AssetKey
    {
        get; set;
    }
    public int? Version
    {
        get; set;
    }
    public required string CompositionVersions
    {
        get; set;
    }
    public required string Critique
    {
        get; set;
    }
    public string? Correction
    {
        get; set;
    }
    public bool UseForFutureArtwork { get; set; }
    public bool Regenerate
    {
        get; set;
    }
    /// <summary>Exact approved image version reviewed or explicitly selected for replacement.</summary>
    public Guid? SourceArtAssetId
    {
        get; set;
    }
    /// <summary>Immutable SQL prompt revision saved or pinned by this review.</summary>
    public Guid? PromptRevisionId
    {
        get; set;
    }
    /// <summary>Durable image job created for an explicit selected replacement.</summary>
    public Guid? RegenerationWorkJobId
    {
        get; set;
    }
    public DateTimeOffset CreatedAt
    {
        get; set;
    }
}
