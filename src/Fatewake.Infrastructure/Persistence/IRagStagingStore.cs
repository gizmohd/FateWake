namespace Fatewake.Infrastructure.Persistence;

/// <summary>Durably stages RAG uploads for Worker containers through PostgreSQL.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/IRagStagingStore.md">IRagStagingStore documentation</see>.</remarks>
public interface IRagStagingStore
{
    Task<RagStagingRecord> PutAsync(string contentHash, string fileName, string contentType, byte[] bytes, CancellationToken ct = default);
    Task<RagStagingRecord?> GetAsync(Guid id, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
