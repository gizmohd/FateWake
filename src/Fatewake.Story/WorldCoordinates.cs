namespace Fatewake.Story;

/// <summary>A coarse axial hex cell on a single Realm's map.</summary>
public sealed record RealmCell(Guid RealmId, int Q, int R)
{
    /// <summary>Tests adjacency without joining unrelated Realm grids.</summary>
    public bool IsNeighbor(RealmCell other)
    {
        var q = (long)Q - other.Q;
        var r = (long)R - other.R;
        return RealmId == other.RealmId && Math.Max(Math.Max(Math.Abs(q), Math.Abs(r)), Math.Abs(q + r)) == 1;
    }
}

/// <summary>A location on a specific Thread and world clock. Ticks are in-world minutes from the clock's epoch.</summary>
public sealed record WorldPosition(Guid TimelineId, RealmCell Cell, string ClockKey, long Tick)
{
    /// <summary>Exact temporal and spatial overlap on a shared Thread.</summary>
    public bool IsCoPresent(WorldPosition other) => this == other;
}

/// <summary>Recorded destination and the in-game time it was reached.</summary>
public sealed record WorldVisit(long Sequence, string Place, string Realm, WorldPosition Position, bool IsHop = false);

/// <summary>A survivor's active location, recent path and names visible at their current position.</summary>
public sealed record WorldMap(WorldVisit? Current, IReadOnlyList<WorldVisit> Visits, IReadOnlyList<string> CoPresent);
