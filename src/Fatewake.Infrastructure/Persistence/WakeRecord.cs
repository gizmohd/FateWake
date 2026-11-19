namespace Fatewake.Infrastructure.Persistence;

/// <summary>A branch-owned consequence projection retaining its immutable causal origin.</summary>
/// <see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/WakeRecord.md">Documentation</see>
public sealed class WakeRecord
{
    public Guid Id
    {
        get; set;
    }
    public Guid TimelineId
    {
        get; set;
    }
    public Guid OriginEventId
    {
        get; set;
    }
    public required string WakeType
    {
        get; set;
    }
    public required string Scope
    {
        get; set;
    }
    public string? TargetEntityType
    {
        get; set;
    }
    public Guid? TargetEntityId
    {
        get; set;
    }
    public short Severity
    {
        get; set;
    }
    public required WakeState State
    {
        get; set;
    }
    public required string Properties
    {
        get; set;
    }
    public int CreatedDay
    {
        get; set;
    }
    public DateTimeOffset CreatedAt
    {
        get; set;
    }
}
