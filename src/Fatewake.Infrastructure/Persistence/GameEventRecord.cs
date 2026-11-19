namespace Fatewake.Infrastructure.Persistence;

/// <summary>Append-only canonical history; ancestry refers to original events without replaying their effects.</summary>
/// <see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/GameEventRecord.md">Documentation</see>
public sealed class GameEventRecord
{
    public Guid Id
    {
        get; set;
    }
    public Guid TimelineId
    {
        get; set;
    }
    public Guid? SurvivorId
    {
        get; set;
    }
    public required string EventType
    {
        get; set;
    }
    public long Sequence
    {
        get; set;
    }
    public int SurvivorDay
    {
        get; set;
    }
    public Guid? CausationEventId
    {
        get; set;
    }
    public Guid CorrelationId
    {
        get; set;
    }
    public required string Payload
    {
        get; set;
    }
    public int SchemaVersion { get; set; } = 1;
    public DateTimeOffset OccurredAt
    {
        get; set;
    }
}
