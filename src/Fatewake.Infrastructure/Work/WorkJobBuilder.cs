using Fatewake.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fatewake.Infrastructure.Work;

/// <summary>Creates durable dependency-aware jobs transactionally and signals initially runnable steps.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Work/WorkJobBuilder.md">WorkJobBuilder documentation</see>. PostgreSQL remains authoritative; signal publication is best-effort.</remarks>
public sealed class WorkJobBuilder(FatewakeDbContext db,IWorkSignalBus signals,ILogger<WorkJobBuilder> log):IWorkJobBuilder
{
    public async Task<CreatedWorkJob> CreateAsync(WorkJobDefinition definition,CancellationToken ct=default)
    {
        Validate(definition);
        var existing=await db.WorkJobs.AsNoTracking().SingleOrDefaultAsync(x=>x.JobType==definition.JobType&&x.IdempotencyKey==definition.IdempotencyKey,ct);
        if(existing is not null)
        {
            var existingSteps=await db.WorkSteps.AsNoTracking().Where(x=>x.JobId==existing.Id).ToDictionaryAsync(x=>x.StepType+"|"+x.Queue+"|"+x.Id,x=>x.Id,ct);
            return new(existing.Id,true,existingSteps);
        }

        var now=DateTimeOffset.UtcNow;var jobId=Guid.NewGuid();
        var ids=definition.Steps.ToDictionary(x=>x.Key,_=>Guid.NewGuid(),StringComparer.Ordinal);
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        db.WorkJobs.Add(new(){Id=jobId,JobType=definition.JobType,IdempotencyKey=definition.IdempotencyKey,Status=WorkJobStatus.Pending,
            Priority=definition.Priority,Payload=definition.Payload,CreatedAt=now,UpdatedAt=now});
        foreach(var s in definition.Steps)
        {
            var ready=s.DependsOn is null||s.DependsOn.Count==0;
            db.WorkSteps.Add(new(){Id=ids[s.Key],JobId=jobId,StepType=s.StepType,Queue=s.Queue,Input=s.Input,Priority=s.Priority,
                MaxAttempts=s.MaxAttempts,Status=ready?WorkStepStatus.Ready:WorkStepStatus.Pending,CreatedAt=now,UpdatedAt=now});
            foreach(var dep in s.DependsOn??[])db.WorkStepDependencies.Add(new(){StepId=ids[s.Key],DependsOnStepId=ids[dep]});
        }
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);

        foreach(var s in definition.Steps.Where(x=>x.DependsOn is null||x.DependsOn.Count==0))
            try{await signals.SignalAsync(s.Queue,ids[s.Key],ct);}
            catch(OperationCanceledException) when(ct.IsCancellationRequested){throw;}
            catch(Exception ex){log.LogWarning(ex,"Work step {StepId} persisted but RabbitMQ signal failed; polling will recover it",ids[s.Key]);}
        return new(jobId,false,ids);
    }

    private static void Validate(WorkJobDefinition d)
    {
        if(d.Steps.Count==0)throw new ArgumentException("A job requires at least one step.");
        var keys=d.Steps.Select(x=>x.Key).ToHashSet(StringComparer.Ordinal);
        if(keys.Count!=d.Steps.Count)throw new ArgumentException("Step keys must be unique.");
        foreach(var s in d.Steps)foreach(var dep in s.DependsOn??[])if(!keys.Contains(dep))throw new ArgumentException($"Unknown dependency '{dep}'.");
        var visiting=new HashSet<string>(StringComparer.Ordinal);var visited=new HashSet<string>(StringComparer.Ordinal);
        bool Cycle(string key){if(!visiting.Add(key))return true;if(visited.Contains(key)){visiting.Remove(key);return false;}
            var step=d.Steps.Single(x=>x.Key==key);foreach(var dep in step.DependsOn??[])if(Cycle(dep))return true;
            visiting.Remove(key);visited.Add(key);return false;}
        foreach(var key in keys)if(Cycle(key))throw new ArgumentException("Work dependency graph contains a cycle.");
    }
}
