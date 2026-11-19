using Fatewake.GameEngine;

namespace Fatewake.Infrastructure.Persistence;

/// <summary>Persists accepted deterministic outcomes exactly once on the selected branch, rejecting conflicting retries.</summary>
/// <see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/IResolutionStore.md">Documentation</see>
public interface IResolutionStore
{
    Task<ActionResolution> PersistAsync(Guid eventInstanceId, Guid survivorId, Guid timelineId, Guid idempotencyKey,
        CandidateAction action, ActionResolution resolution, CancellationToken cancellationToken = default);
}
