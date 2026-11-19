namespace Fatewake.Infrastructure.Persistence;

/// <summary>Persists and updates preferred-provider artwork regeneration requests.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/IArtRegenerationStore.md">IArtRegenerationStore documentation</see>.</remarks>
public interface IArtRegenerationStore
{
    Task<ArtRegenerationRecord> QueueAsync(Guid sourceWorkJobId, string visualFingerprint, string requestJson, string targetProvider, string? targetModel, string reason, CancellationToken ct = default);
}
