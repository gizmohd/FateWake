namespace Fatewake.Infrastructure.Persistence;

/// <summary>Identifies an authorized corpus source that RAG ingestion may index.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/RagSourceRecord.md">RagSourceRecord documentation</see>.</remarks>
public sealed class RagSourceRecord
{
    public Guid Id { get; set; }
    public required string SourceKey { get; set; }
    public required string SourceType { get; set; }
    public required string Title { get; set; }
    public required string Origin { get; set; }
    public string? CanonicalUri { get; set; }
    public required string ContentHash { get; set; }
    public required string LicenseStatus { get; set; }
    public required string ReviewStatus { get; set; }
    public required string Visibility { get; set; }
    public string Metadata { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
