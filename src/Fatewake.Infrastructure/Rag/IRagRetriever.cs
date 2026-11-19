namespace Fatewake.Infrastructure.Rag;

/// <summary>Finds approved, authorized passages relevant to a query.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Rag/IRagRetriever.md">Documentation</see>.</remarks>
public interface IRagRetriever
{
    /// <summary>Returns at most <paramref name="limit"/> passages ordered by relevance.</summary>
    Task<IReadOnlyList<RagPassage>> SearchAsync(string query, RagSearchFilter filter, int limit, CancellationToken ct);
}
