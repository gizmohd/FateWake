namespace Fatewake.Web.Presentation;

/// <summary>API view of RAG corpus counts and recent ingestion runs.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Web/Presentation/RagStatusResultDto.md">RagStatusResultDto documentation</see>.</remarks>
public sealed record RagStatusResultDto(
    int SourceCount,
    int DocumentCount,
    int ChunkCount,
    int EmbeddedChunkCount,
    int ClaimCount,
    int ApprovedClaimCount,
    RagRunStatusDto[] Runs);
