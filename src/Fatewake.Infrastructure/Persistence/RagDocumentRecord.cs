namespace Fatewake.Infrastructure.Persistence;

/// <summary>Represents one versioned manuscript, document, code artifact, or generated-story file in the RAG corpus.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/RagDocumentRecord.md">RagDocumentRecord documentation</see>.</remarks>
public sealed class RagDocumentRecord
{
    public Guid Id { get; set; }
    public Guid SourceId { get; set; }
    public required string DocumentKey { get; set; }
    public required string DocumentType { get; set; }
    public required string Title { get; set; }
    public string Language { get; set; } = "en";
    public int Sequence { get; set; }
    public required string ContentHash { get; set; }
    public string Metadata { get; set; } = "{}";
    public required string Status { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
