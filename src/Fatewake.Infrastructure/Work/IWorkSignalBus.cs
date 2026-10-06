namespace Fatewake.Infrastructure.Work;
/// <summary>Signals that durable work is available without becoming the source of truth for that work.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Work/IWorkSignalBus.md">IWorkSignalBus documentation</see>.</remarks>
public interface IWorkSignalBus
{
    /// <summary>Signals a queue that a durable step may be claimed.</summary>
    Task SignalAsync(string queue,Guid stepId,CancellationToken ct=default);
}