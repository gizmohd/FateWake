namespace Fatewake.Infrastructure.Persistence;

public sealed class WorldCellRecord
{
    public Guid Id { get; set; }
    public Guid RealmId { get; set; }
    public int Q { get; set; }
    public int R { get; set; }
    public required string Name { get; set; }
}

public sealed class WorldVisitRecord
{
    public Guid SurvivorId { get; set; }
    public Guid TimelineId { get; set; }
    public long Sequence { get; set; }
    public Guid CellId { get; set; }
    public required string ClockKey { get; set; }
    public long Tick { get; set; }
    public bool Authored { get; set; }
}
