namespace Fatewake.Infrastructure.Persistence;

/// <summary>Summarizes RAG corpus counts, import runs, and embedding coverage.</summary>
public sealed record RagStatusResult(
    int SourceCount,
    int DocumentCount,
    int ChunkCount,
    int EmbeddedChunkCount,
    int ClaimCount,
    int ApprovedClaimCount,
    RagRunStatus[] Runs);
