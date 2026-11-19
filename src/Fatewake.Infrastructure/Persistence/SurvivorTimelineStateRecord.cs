namespace Fatewake.Infrastructure.Persistence;

/// <summary>Owns mutable canonical facts for one branch; survivor identity and account ownership remain shared.</summary>
/// <see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/SurvivorTimelineStateRecord.md">Documentation</see>
public sealed class SurvivorTimelineStateRecord
{
    public Guid SurvivorId
    {
        get; set;
    }
    public Guid TimelineId
    {
        get; set;
    }
    public int SurvivorDay { get; set; } = 1;
    public SurvivorStatus Status { get; set; } = SurvivorStatus.Active;
    public string Facts { get; set; } = "{}";
    public long Version
    {
        get; set;
    }
}
