using Microsoft.EntityFrameworkCore;

namespace Fatewake.Infrastructure.Persistence;

/// <summary>Implements PostgreSQL-backed leasing and state transitions for horizontally distributed workers.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/WorkStore.md">WorkStore documentation</see>. Claims use row locking with SKIP LOCKED and lease tokens fence stale workers.</remarks>
public sealed class WorkStore(FatewakeDbContext db,Fatewake.Infrastructure.Work.IWorkSignalBus signals):IWorkStore
{
    public async Task<WorkLease?> ClaimAsync(string queue,string workerId,TimeSpan leaseDuration,CancellationToken ct=default)
    {
        var now=DateTimeOffset.UtcNow;
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        var step=await db.WorkSteps.FromSqlInterpolated($@"
            SELECT * FROM work_step
            WHERE ""Queue""={queue}
              AND (""Status""={(int)WorkStepStatus.Ready} OR (""Status""={(int)WorkStepStatus.Leased} AND ""LeaseExpiresAt"" < {now}))
              AND (""NextEligibleAt"" IS NULL OR ""NextEligibleAt"" <= {now})
            ORDER BY ""Priority"" DESC, ""CreatedAt""
            FOR UPDATE SKIP LOCKED LIMIT 1").SingleOrDefaultAsync(ct);
        if(step is null){await tx.CommitAsync(ct);return null;}
        var token=Guid.NewGuid();step.Status=WorkStepStatus.Leased;step.LeaseOwner=workerId;step.LeaseToken=token;
        step.LeaseExpiresAt=now+leaseDuration;step.LastHeartbeatAt=now;step.AttemptCount++;step.UpdatedAt=now;
        var job=await db.WorkJobs.SingleAsync(x=>x.Id==step.JobId,ct);if(job.Status==WorkJobStatus.Pending)job.Status=WorkJobStatus.Running;job.UpdatedAt=now;
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
        return new(step.Id,step.JobId,step.StepType,step.Queue,step.Input,token,step.AttemptCount);
    }

    public async Task<bool> HeartbeatAsync(Guid stepId,Guid token,TimeSpan extendBy,CancellationToken ct=default)
    {
        var now=DateTimeOffset.UtcNow;var step=await db.WorkSteps.SingleOrDefaultAsync(x=>x.Id==stepId&&x.LeaseToken==token&&x.Status==WorkStepStatus.Leased,ct);
        if(step is null)return false;step.LastHeartbeatAt=now;step.LeaseExpiresAt=now+extendBy;step.UpdatedAt=now;await db.SaveChangesAsync(ct);return true;
    }

    public async Task<bool> CompleteAsync(Guid stepId,Guid token,string? output,CancellationToken ct=default)
    {
        var now=DateTimeOffset.UtcNow;var step=await db.WorkSteps.SingleOrDefaultAsync(x=>x.Id==stepId&&x.LeaseToken==token&&x.Status==WorkStepStatus.Leased,ct);
        if(step is null)return false;step.Status=WorkStepStatus.Completed;step.Output=output;step.CompletedAt=now;step.UpdatedAt=now;ClearLease(step);await db.SaveChangesAsync(ct);
        await PromoteReadyStepsAsync(ct);
        if(!await db.WorkSteps.AnyAsync(x=>x.JobId==step.JobId&&x.Status!=WorkStepStatus.Completed&&x.Status!=WorkStepStatus.Cancelled,ct))
        {var job=await db.WorkJobs.SingleAsync(x=>x.Id==step.JobId,ct);job.Status=WorkJobStatus.Completed;job.CompletedAt=now;job.UpdatedAt=now;await db.SaveChangesAsync(ct);}
        return true;
    }

    public async Task<bool> FailAsync(Guid stepId,Guid token,string errorCode,string? detail,TimeSpan retryDelay,CancellationToken ct=default)
    {
        var now=DateTimeOffset.UtcNow;var step=await db.WorkSteps.SingleOrDefaultAsync(x=>x.Id==stepId&&x.LeaseToken==token&&x.Status==WorkStepStatus.Leased,ct);
        if(step is null)return false;step.ErrorCode=errorCode;step.ErrorDetail=detail;step.UpdatedAt=now;ClearLease(step);
        if(step.AttemptCount>=step.MaxAttempts){step.Status=WorkStepStatus.Failed;var job=await db.WorkJobs.SingleAsync(x=>x.Id==step.JobId,ct);job.Status=WorkJobStatus.Failed;job.UpdatedAt=now;}
        else{step.Status=WorkStepStatus.Ready;step.NextEligibleAt=now+retryDelay;}
        await db.SaveChangesAsync(ct);return true;
    }

    public async Task<int> PromoteReadyStepsAsync(CancellationToken ct=default)
    {
        var pending=await db.WorkSteps.Where(x=>x.Status==WorkStepStatus.Pending||x.Status==WorkStepStatus.Retry).ToListAsync(ct);var changed=0;var now=DateTimeOffset.UtcNow;
        foreach(var step in pending)
        {
            var blockers=await db.WorkStepDependencies.Where(d=>d.StepId==step.Id)
                .Join(db.WorkSteps,d=>d.DependsOnStepId,s=>s.Id,(d,s)=>s).AnyAsync(s=>s.Status!=WorkStepStatus.Completed,ct);
            if(!blockers&&(step.NextEligibleAt is null||step.NextEligibleAt<=now)){step.Status=WorkStepStatus.Ready;step.UpdatedAt=now;changed++;}
        }
        if(changed>0)
        {
            await db.SaveChangesAsync(ct);
            foreach(var step in pending.Where(x=>x.Status==WorkStepStatus.Ready))
                try{await signals.SignalAsync(step.Queue,step.Id,ct);}catch{ /* polling remains recovery path */ }
        }
        return changed;
    }
    private static void ClearLease(WorkStepRecord s){s.LeaseOwner=null;s.LeaseToken=null;s.LeaseExpiresAt=null;s.LastHeartbeatAt=null;}
}
