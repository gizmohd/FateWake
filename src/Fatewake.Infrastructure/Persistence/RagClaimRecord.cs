namespace Fatewake.Infrastructure.Persistence;

/// <summary>Records human-reviewable extracted canon or generated-content claims with source provenance.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/RagClaimRecord.md">RagClaimRecord documentation</see>.</remarks>
public sealed class RagClaimRecord
{
    public Guid Id
    {
        get; set;
    }
    public Guid? SourceChunkId
    {
        get; set;
    }
    public Guid? SourceDocumentId
    {
        get; set;
    }
    public required string ClaimType
    {
        get; set;
    }
    public required string Subject
    {
        get; set;
    }
    public string? Predicate
    {
        get; set;
    }
    public string? Object
    {
        get; set;
    }
    public decimal Confidence
    {
        get; set;
    }
    public required string Status
    {
        get; set;
    }
    public string? ApprovedBy
    {
        get; set;
    }
    public DateTimeOffset? ApprovedAt
    {
        get; set;
    }
    public string Metadata { get; set; } = "{}";
    public DateTimeOffset CreatedAt
    {
        get; set;
    }
    public DateTimeOffset UpdatedAt
    {
        get; set;
    }
}
