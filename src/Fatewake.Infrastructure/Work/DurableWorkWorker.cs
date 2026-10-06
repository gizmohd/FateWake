using Fatewake.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Fatewake.Observability;

namespace Fatewake.Infrastructure.Work;

// Polling is the durable recovery path. RabbitMQ signals may wake workers sooner, but correctness never depends on a message.
/// <summary>Executes durable queue work across horizontally scaled worker processes using PostgreSQL leases.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Work/DurableWorkWorker.md">DurableWorkWorker documentation</see>. Active handlers renew their leases to prevent duplicate claims during long operations.</remarks>
public sealed class DurableWorkWorker(IServiceScopeFactory scopes,IOptions<WorkExecutionOptions> options,WorkQueueWakeup wakeup,ILogger<DurableWorkWorker> log):BackgroundService
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
                if(lease is null){await wakeup.WaitAsync(queue,options.Value.PollInterval,ct);continue;}
                using var operation = OperationTelemetry.Start("work.execute", log);
                using var logScope = log.BeginScope(new Dictionary<string, object> { ["JobId"] = lease.JobId, ["StepId"] = lease.StepId, ["Queue"] = queue });
                var handler=scope.ServiceProvider.GetServices<IWorkStepHandler>().SingleOrDefault(x=>x.StepType==lease.StepType);
                if(handler is null)
                {
                    operation.Fail(new InvalidOperationException("Work handler is missing."));
                    log.LogError("No handler for work step type {StepType}", lease.StepType);
                    await store.FailAsync(lease.StepId,lease.LeaseToken,"handler_missing",$"No handler for {lease.StepType}",TimeSpan.Zero,ct);
                    continue;
                }
                try
                {
                    using var executionCts=CancellationTokenSource.CreateLinkedTokenSource(ct);
                    var heartbeat=HeartbeatAsync(lease,options.Value.LeaseDuration,executionCts,executionCts.Token);
                    string? output;
                    try{output=await handler.ExecuteAsync(new WorkStepExecutionContext(lease.JobId,lease.StepId,lease.Input,lease.Attempt),executionCts.Token);}
                    finally{executionCts.Cancel();try{await heartbeat;}catch(OperationCanceledException){ }}
                    await store.CompleteAsync(lease.StepId,lease.LeaseToken,output,ct);
                }
                catch(OperationCanceledException ex) when(ct.IsCancellationRequested){operation.Fail(ex);throw;}
                catch(Exception ex)
                {
                    operation.Fail(ex);
                    log.LogError(ex,"Work step {StepId} failed on {WorkerId}",lease.StepId,workerId);
                    await store.FailAsync(lease.StepId,lease.LeaseToken,"execution_failed",ex.Message,TimeSpan.FromSeconds(Math.Min(300,Math.Pow(2,lease.Attempt)*5)),ct);
                }
            }
            catch(OperationCanceledException) when(ct.IsCancellationRequested){break;}
            catch(Exception ex){log.LogError(ex,"Queue worker {WorkerId} failed",workerId);await Task.Delay(options.Value.PollInterval,ct);}
        }
    }

    /// <summary>Renews a lease while a potentially long-running handler is executing.</summary>
    private async Task HeartbeatAsync(WorkLease lease,TimeSpan leaseDuration,CancellationTokenSource executionCts,CancellationToken ct)
    {
        var interval=TimeSpan.FromTicks(Math.Max(TimeSpan.FromSeconds(5).Ticks,leaseDuration.Ticks/3));
        while(!ct.IsCancellationRequested)
        {
            await Task.Delay(interval,ct);
            using var scope=scopes.CreateScope();
            var store=scope.ServiceProvider.GetRequiredService<IWorkStore>();
            if(!await store.HeartbeatAsync(lease.StepId,lease.LeaseToken,leaseDuration,ct))
            {
                executionCts.Cancel();
                throw new InvalidOperationException($"Lost lease for work step {lease.StepId}.");
            }
        }
    }
}
