namespace Fatewake.Infrastructure.Persistence;

/// <summary>Reports that a RAG upload was staged and durably queued for Worker processing.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/RagQueuedResult.md">RagQueuedResult documentation</see>.</remarks>
public sealed record RagQueuedResult(Guid WorkJobId, Guid StagingId, bool ReusedJob);
