namespace Fatewake.Infrastructure.Persistence;

/// <summary>Preserves the immutable origin of a personal timeline without duplicating survivor identity.</summary>
/// <see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/TimelineBranchRecord.md">Documentation</see>
public sealed class TimelineBranchRecord
{
    public Guid TimelineId
    {
        get; set;
    }
    public Guid SurvivorId
    {
        get; set;
    }
    public Guid RootTimelineId
    {
        get; set;
    }
    public Guid? ParentTimelineId
    {
        get; set;
    }
    public Guid? CheckpointId
    {
        get; set;
    }
    public Guid? RequestId
    {
        get; set;
    }
    public string Origin { get; set; } = "original";
    public DateTimeOffset CreatedAt
    {
        get; set;
    }
}
