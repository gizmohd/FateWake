using System.Collections.Concurrent;

namespace Fatewake.Infrastructure.Work;

/// <summary>Coalesces queue wakeup hints while retaining a timed PostgreSQL polling path.</summary>
/// <see href="../../../docs/code/src/Fatewake.Infrastructure/Work/WorkQueueWakeup.md">WorkQueueWakeup documentation</see>
public sealed class WorkQueueWakeup : IDisposable
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _queues = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Wakes an idle worker, coalescing duplicate hints for the same queue.</summary>
    /// <param name="queue">Logical queue name.</param>
    public void Signal(string queue)
    {
        var semaphore = _queues.GetOrAdd(queue, _ => new SemaphoreSlim(0, 1));
        try { semaphore.Release(); }
        catch (SemaphoreFullException) { /* A wakeup is already pending. */ }
    }

    /// <summary>Waits for a signal or the recovery polling interval, whichever occurs first.</summary>
    /// <param name="queue">Logical queue name.</param>
    /// <param name="pollInterval">Maximum time to wait before polling PostgreSQL.</param>
    /// <param name="ct">Cancellation token used during worker shutdown.</param>
    /// <returns>A task completed when the worker should poll again.</returns>
    public async Task WaitAsync(string queue, TimeSpan pollInterval, CancellationToken ct)
    {
        var semaphore = _queues.GetOrAdd(queue, _ => new SemaphoreSlim(0, 1));
        await semaphore.WaitAsync(pollInterval, ct);
    }

    /// <summary>Releases queue semaphores after consumers and workers have stopped.</summary>
    public void Dispose()
    {
        foreach (var semaphore in _queues.Values) semaphore.Dispose();
    }
}
