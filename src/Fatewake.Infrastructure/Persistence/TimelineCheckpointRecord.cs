namespace Fatewake.Infrastructure.Persistence;

/// <summary>Freezes canonical state and its disclosure boundary at an actually reached restore point.</summary>
/// <see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/TimelineCheckpointRecord.md">Documentation</see>
public sealed class TimelineCheckpointRecord
{
    public Guid Id
    {
        get; set;
    }
    public Guid TimelineId
    {
        get; set;
    }
    public Guid SurvivorId
    {
        get; set;
    }
    public Guid EventInstanceId
    {
        get; set;
    }
    public required string Key
    {
        get; set;
    }
    public required string Label
    {
        get; set;
    }
    public required string TimeLabel
    {
        get; set;
    }
    public required string Snapshot
    {
        get; set;
    }
    public required string SnapshotHash
    {
        get; set;
    }
    public DateTimeOffset CreatedAt
    {
        get; set;
    }
}
