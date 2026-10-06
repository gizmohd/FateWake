namespace Fatewake.Infrastructure.Persistence;
/// <summary>Reads and idempotently publishes durable job-scoped JSON artifacts for communication between worker replicas.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/IWorkArtifactStore.md">IWorkArtifactStore documentation</see>.</remarks>
public interface IWorkArtifactStore
{
    Task<string?> GetAsync(Guid jobId,string key,CancellationToken ct=default);
    Task PutAsync(Guid jobId,string key,string value,CancellationToken ct=default);
}