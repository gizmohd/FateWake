namespace Fatewake.Infrastructure.Persistence;
/// <summary>Persists one independently leasable unit of distributed work.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/WorkStepRecord.md">WorkStepRecord documentation</see>.</remarks>
public sealed class WorkStepRecord
{
    public Guid Id{get;set;} public Guid JobId{get;set;} public required string StepType{get;set;} public required string Queue{get;set;}
    public WorkStepStatus Status{get;set;} public int Priority{get;set;} public int AttemptCount{get;set;} public int MaxAttempts{get;set;}=5;
    public DateTimeOffset? NextEligibleAt{get;set;} public string Input{get;set;}="{}"; public string? Output{get;set;}
    public string? LeaseOwner{get;set;} public Guid? LeaseToken{get;set;} public DateTimeOffset? LeaseExpiresAt{get;set;}
    public DateTimeOffset? LastHeartbeatAt{get;set;} public string? ErrorCode{get;set;} public string? ErrorDetail{get;set;}
    public DateTimeOffset CreatedAt{get;set;} public DateTimeOffset UpdatedAt{get;set;} public DateTimeOffset? CompletedAt{get;set;}
}