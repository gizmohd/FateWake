namespace Fatewake.Infrastructure.Work;

public sealed class WorkExecutionOptions
{
    public TimeSpan LeaseDuration{get;set;}=TimeSpan.FromMinutes(5);
    public TimeSpan PollInterval{get;set;}=TimeSpan.FromSeconds(5);
    public Dictionary<string,int> QueueConcurrency{get;set;}=new(StringComparer.OrdinalIgnoreCase)
    {
        ["art.generate"]=1,["art.encode"]=2,["art.validate"]=2,["narrative.render"]=2,["maintenance"]=1
    };
}

public interface IWorkStepHandler
{
    string StepType{get;}
    Task<string?> ExecuteAsync(string input,CancellationToken ct);
}

public interface IWorkSignalBus
{
    Task SignalAsync(string queue,Guid stepId,CancellationToken ct=default);
}

public sealed class NullWorkSignalBus:IWorkSignalBus
{
    public Task SignalAsync(string queue,Guid stepId,CancellationToken ct=default)=>Task.CompletedTask;
}
