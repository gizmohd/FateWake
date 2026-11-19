namespace Fatewake.Infrastructure.Persistence;

/// <summary>Stores retrievable passage text and provenance without conflating a passage with canonical truth.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/RagChunkRecord.md">RagChunkRecord documentation</see>.</remarks>
public sealed class RagChunkRecord
{
    public Guid Id
    {
        get; set;
    }
    public Guid DocumentId
    {
        get; set;
    }
    public required string ChunkKey
    {
        get; set;
    }
    public int Ordinal
    {
        get; set;
    }
    public string? HeadingPath
    {
        get; set;
    }
    public required string Text
    {
        get; set;
    }
    public int? StartOffset
    {
        get; set;
    }
    public int? EndOffset
    {
        get; set;
    }
    public string? TimelineKey
    {
        get; set;
    }
    public int? SurvivorDayMin
    {
        get; set;
    }
    public int? SurvivorDayMax
    {
        get; set;
    }
    public int? SpoilerSeriesMin
    {
        get; set;
    }
    public int? SpoilerSeriesMax
    {
        get; set;
    }
    public required string CanonStatus
    {
        get; set;
    }
    public required string ReviewStatus
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
    public DateTimeOffset? EmbeddedAt
    {
        get; set;
    }
    public DateTimeOffset CreatedAt
    {
        get; set;
    }
    public DateTimeOffset UpdatedAt
    {
        get; set;
    }
}
