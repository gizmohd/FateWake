namespace Fatewake.Infrastructure.Persistence;
/// <summary>Persists the authoritative state and idempotency identity of a distributed work job.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/WorkJobRecord.md">WorkJobRecord documentation</see>.</remarks>
public sealed class WorkJobRecord
{
    public Guid Id{get;set;} public required string JobType{get;set;} public required string IdempotencyKey{get;set;}
    public WorkJobStatus Status{get;set;} public int Priority{get;set;} public string Payload{get;set;}="{}";
    public DateTimeOffset CreatedAt{get;set;} public DateTimeOffset UpdatedAt{get;set;} public DateTimeOffset? CompletedAt{get;set;}
}