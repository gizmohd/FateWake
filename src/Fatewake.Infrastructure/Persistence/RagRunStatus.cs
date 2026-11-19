namespace Fatewake.Infrastructure.Persistence;

/// <summary>Describes a single RAG ingestion run for status interfaces.</summary>
public sealed record RagRunStatus(
    Guid Id,
    string RunKey,
    string Phase,
    string Status,
    string? IngestorVersion,
    string? EmbeddingModel,
    int SourceCount,
    int DocumentCount,
    int ChunkCount,
    string? ErrorDetail,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt);
