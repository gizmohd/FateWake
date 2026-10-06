namespace Fatewake.Infrastructure.Work;
/// <summary>Provides a no-op signal transport so PostgreSQL polling can operate without a broker.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Work/NullWorkSignalBus.md">NullWorkSignalBus documentation</see>.</remarks>
public sealed class NullWorkSignalBus:IWorkSignalBus
{
    /// <inheritdoc/>
    public Task SignalAsync(string queue,Guid stepId,CancellationToken ct=default)=>Task.CompletedTask;
}