namespace Fatewake.Web.Presentation;

/// <summary>API view of one RAG ingestion run.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Web/Presentation/RagRunStatusDto.md">RagRunStatusDto documentation</see>.</remarks>
public sealed record RagRunStatusDto(
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
