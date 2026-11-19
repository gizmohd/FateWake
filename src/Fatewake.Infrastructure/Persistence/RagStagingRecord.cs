namespace Fatewake.Infrastructure.Persistence;

/// <summary>Stores a temporary uploaded RAG document in PostgreSQL for cross-container worker processing.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/RagStagingRecord.md">RagStagingRecord documentation</see>.</remarks>
public sealed class RagStagingRecord
{
    public Guid Id { get; set; }
    public required string ContentHash { get; set; }
    public required string FileName { get; set; }
    public required string ContentType { get; set; }
    public required byte[] Bytes { get; set; }
    public long ByteSize { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
