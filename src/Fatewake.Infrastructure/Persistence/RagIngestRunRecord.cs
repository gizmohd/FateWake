namespace Fatewake.Infrastructure.Persistence;

/// <summary>Tracks reproducible RAG corpus ingestion and reindex work independently of process lifetime.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/RagIngestRunRecord.md">RagIngestRunRecord documentation</see>.</remarks>
public sealed class RagIngestRunRecord
{
    public Guid Id
    {
        get; set;
    }
    public required string RunKey
    {
        get; set;
    }
    public required string Phase
    {
        get; set;
    }
    public required string Status
    {
        get; set;
    }
    public string? IngestorVersion
    {
        get; set;
    }
    public string? EmbeddingModel
    {
        get; set;
    }
    public int? EmbeddingDimensions
    {
        get; set;
    }
    public int SourceCount
    {
        get; set;
    }
    public int DocumentCount
    {
        get; set;
    }
    public int ChunkCount
    {
        get; set;
    }
    public string? ErrorDetail
    {
        get; set;
    }
    public DateTimeOffset StartedAt
    {
        get; set;
    }
    public DateTimeOffset? CompletedAt
    {
        get; set;
    }
}
