using Fatewake.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fatewake.Infrastructure.Work;

// Polling is the durable recovery path. RabbitMQ signals may wake workers sooner, but correctness never depends on a message.
/// <summary>Executes durable queue work across horizontally scaled worker processes using PostgreSQL leases.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Work/DurableWorkWorker.md">DurableWorkWorker documentation</see>. Active handlers renew their leases to prevent duplicate claims during long operations.</remarks>
public sealed class DurableWorkWorker(IServiceScopeFactory scopes,IOptions<WorkExecutionOptions> options,ILogger<DurableWorkWorker> log):BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var tasks=options.Value.QueueConcurrency.Where(x=>x.Value>0)
            .SelectMany(x=>Enumerable.Range(0,x.Value).Select(i=>RunQueueAsync(x.Key,$"{Environment.MachineName}:{Environment.ProcessId}:{x.Key}:{i}",stoppingToken)));
        return Task.WhenAll(tasks);
    }

    private async Task RunQueueAsync(string queue,string workerId,CancellationToken ct)
    {
        while(!ct.IsCancellationRequested)
        {
            try
            {
                using var scope=scopes.CreateScope();var store=scope.ServiceProvider.GetRequiredService<IWorkStore>();
                await store.PromoteReadyStepsAsync(ct);
                var lease=await store.ClaimAsync(queue,workerId,options.Value.LeaseDuration,ct);
                if(lease is null){await Task.Delay(options.Value.PollInterval,ct);continue;}
                var handler=scope.ServiceProvider.GetServices<IWorkStepHandler>().SingleOrDefault(x=>x.StepType==lease.StepType);
                if(handler is null){await store.FailAsync(lease.StepId,lease.LeaseToken,"handler_missing",$"No handler for {lease.StepType}",TimeSpan.Zero,ct);continue;}
                try
                {
                    using var executionCts=CancellationTokenSource.CreateLinkedTokenSource(ct);
                    var heartbeat=HeartbeatAsync(store,lease,options.Value.LeaseDuration,executionCts.Token);
                    string? output;
                    try{output=await handler.ExecuteAsync(lease.Input,executionCts.Token);}
                    finally{executionCts.Cancel();try{await heartbeat;}catch(OperationCanceledException){ }}
                    await store.CompleteAsync(lease.StepId,lease.LeaseToken,output,ct);
                }
                catch(OperationCanceledException) when(ct.IsCancellationRequested){throw;}
                catch(Exception ex)
                {
                    log.LogError(ex,"Work step {StepId} failed on {WorkerId}",lease.StepId,workerId);
                    await store.FailAsync(lease.StepId,lease.LeaseToken,"execution_failed",ex.Message,TimeSpan.FromSeconds(Math.Min(300,Math.Pow(2,lease.Attempt)*5)),ct);
                }
            }
            catch(OperationCanceledException) when(ct.IsCancellationRequested){break;}
            catch(Exception ex){log.LogError(ex,"Queue worker {WorkerId} failed",workerId);await Task.Delay(options.Value.PollInterval,ct);}
        }
    }

    /// <summary>Renews a lease while a potentially long-running handler is executing.</summary>
    private static async Task HeartbeatAsync(IWorkStore store,WorkLease lease,TimeSpan leaseDuration,CancellationToken ct)
    {
        var interval=TimeSpan.FromTicks(Math.Max(TimeSpan.FromSeconds(5).Ticks,leaseDuration.Ticks/3));
        while(!ct.IsCancellationRequested)
        {
            await Task.Delay(interval,ct);
            if(!await store.HeartbeatAsync(lease.StepId,lease.LeaseToken,leaseDuration,ct))
                throw new InvalidOperationException($"Lost lease for work step {lease.StepId}.");
        }
    }
}
