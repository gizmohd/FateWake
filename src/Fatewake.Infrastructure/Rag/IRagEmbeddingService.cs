namespace Fatewake.Infrastructure.Rag;

/// <summary>Embeds RAG chunks that do not yet have a vector.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Rag/IRagEmbeddingService.md">Documentation</see>.</remarks>
public interface IRagEmbeddingService
{
    /// <summary>Embeds up to one batch of pending chunks and returns how many were stored; zero when disabled or idle.</summary>
    Task<int> EmbedPendingAsync(CancellationToken ct);
}
