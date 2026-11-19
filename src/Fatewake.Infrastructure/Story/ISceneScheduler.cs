using Fatewake.Infrastructure.Persistence;

namespace Fatewake.Infrastructure.Story;

/// <summary>Reserves scene rows and queues their generation idempotently.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Story/ISceneScheduler.md">Documentation</see>.</remarks>
public interface ISceneScheduler
{
    /// <summary>Ensures the scene at a sequence exists and is queued; reuses any non-failed row and requeues failed ones.</summary>
    /// <param name="survivorId">Survivor owning the timeline.</param>
    /// <param name="timelineId">Timeline receiving the scene.</param>
    /// <param name="sequence">One-based scene number.</param>
    /// <param name="requeueFailed">Requeue a Failed row; otherwise it is returned unchanged.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<GeneratedSceneRecord> EnsureAsync(Guid survivorId, Guid timelineId, int sequence, bool requeueFailed, CancellationToken ct);
}
