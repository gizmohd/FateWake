using Fatewake.Infrastructure.Work;

namespace Fatewake.IntegrationTests;

/// <summary>Verifies queue wakeup coalescing, cancellation, and recovery polling.</summary>
/// <see href="../../docs/code/tests/Fatewake.IntegrationTests/WorkQueueWakeupTests.md">WorkQueueWakeupTests documentation</see>
public sealed class WorkQueueWakeupTests
{
    /// <summary>A pending hint survives until a worker waits, and duplicates are coalesced.</summary>
    [Fact]
    public async Task Pending_signals_wake_workers_without_waiting_for_poll_timeout()
    {
        using var wakeup = new WorkQueueWakeup();
        wakeup.Signal("art.finalize");
        wakeup.Signal("art.finalize");
        var wait = wakeup.WaitAsync("art.finalize", TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);
        Assert.True(wait.IsCompletedSuccessfully);
        await wait;
    }

    /// <summary>Absent hints do not prevent recovery polling.</summary>
    [Fact]
    public async Task Missing_signals_fall_back_to_polling()
    {
        using var wakeup = new WorkQueueWakeup();
        await wakeup.WaitAsync("art.encode", TimeSpan.FromMilliseconds(10), TestContext.Current.CancellationToken);
    }

    /// <summary>Shutdown cancels a waiting worker promptly.</summary>
    [Fact]
    public async Task Wait_is_cancelled_on_shutdown()
    {
        using var wakeup = new WorkQueueWakeup();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var wait = wakeup.WaitAsync("art.encode", TimeSpan.FromMinutes(1), stop.Token);
        stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wait);
    }
}
