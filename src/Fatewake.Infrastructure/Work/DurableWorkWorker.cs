using Fatewake.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fatewake.Infrastructure.Work;

// Polling is the durable recovery path. RabbitMQ signals may wake workers sooner, but correctness never depends on a message.
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
                    var output=await handler.ExecuteAsync(lease.Input,ct);
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
}
