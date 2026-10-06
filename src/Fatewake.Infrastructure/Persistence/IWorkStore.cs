namespace Fatewake.Infrastructure.Persistence;
/// <summary>Provides authoritative PostgreSQL operations for leasing and transitioning durable work.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/IWorkStore.md">IWorkStore documentation</see>.</remarks>
public interface IWorkStore
{
    Task<WorkLease?> ClaimAsync(string queue,string workerId,TimeSpan leaseDuration,CancellationToken ct=default);
    Task<bool> HeartbeatAsync(Guid stepId,Guid leaseToken,TimeSpan extendBy,CancellationToken ct=default);
    Task<bool> CompleteAsync(Guid stepId,Guid leaseToken,string? output,CancellationToken ct=default);
    Task<bool> FailAsync(Guid stepId,Guid leaseToken,string errorCode,string? detail,TimeSpan retryDelay,CancellationToken ct=default);
    Task<int> PromoteReadyStepsAsync(CancellationToken ct=default);
}