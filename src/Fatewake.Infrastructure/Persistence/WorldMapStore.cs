using System.Globalization;
using Fatewake.Story;
using Microsoft.EntityFrameworkCore;

namespace Fatewake.Infrastructure.Persistence;

/// <summary>Records authoritative story positions and projects the owner's active Thread onto Realm grids.</summary>
public sealed class WorldMapStore(FatewakeDbContext db)
{
    private const string SilenceClock = "silence-season-1";

    /// <summary>Reads the recent path and co-present names for the selected Thread.</summary>
    public async Task<WorldMap> ReadAsync(Guid survivorId, CancellationToken ct)
    {
        var survivor = await db.Survivors.AsNoTracking().SingleAsync(row => row.Id == survivorId, ct);
        var visits = db.Set<WorldVisitRecord>().AsNoTracking();
        var recent = await visits.Where(row => row.SurvivorId == survivorId && row.TimelineId == survivor.TimelineId)
            .OrderByDescending(row => row.Sequence).Take(100).ToListAsync(ct);
        if (recent.Count == 0)
        {
            var episode = await db.EventInstances.AsNoTracking().SingleOrDefaultAsync(row =>
                row.SurvivorId == survivorId && row.TimelineId == survivor.TimelineId && row.Status == EventInstanceStatus.Active, ct);
            if (episode is null || AuthoredPosition(SessionStore.ToState(survivor, episode)) is not { } legacy)
                return new(null, [], []);
            recent.Add(legacy);
        }

        var cellIds = recent.Select(row => row.CellId).Distinct().ToArray();
        var cells = await (from cell in db.Set<WorldCellRecord>().AsNoTracking()
                           join realm in db.Realms.AsNoTracking() on cell.RealmId equals realm.Id
                           where cellIds.Contains(cell.Id)
                           select new { cell.Id, cell.Name, cell.RealmId, cell.Q, cell.R, Realm = realm.Name })
            .ToDictionaryAsync(row => row.Id, ct);
        var path = recent.AsEnumerable().Reverse().Select(row =>
        {
            var cell = cells[row.CellId];
            return new WorldVisit(row.Sequence, cell.Name, cell.Realm,
                new(row.TimelineId, new(cell.RealmId, cell.Q, cell.R), row.ClockKey, row.Tick), !row.Authored);
        }).ToArray();
        var latest = recent[0];
        var present = await (from visit in visits
                             join peer in db.Survivors.AsNoTracking() on visit.SurvivorId equals peer.Id
                             where peer.Id != survivorId && peer.Status == SurvivorStatus.Active &&
                                 peer.TimelineId == latest.TimelineId && visit.TimelineId == latest.TimelineId &&
                                 visit.CellId == latest.CellId && visit.ClockKey == latest.ClockKey && visit.Tick == latest.Tick &&
                                 !visits.Any(later => later.SurvivorId == peer.Id && later.TimelineId == visit.TimelineId &&
                                     later.Sequence > visit.Sequence)
                             orderby peer.DisplayName, peer.Id
                             select peer.DisplayName).Take(100).ToArrayAsync(ct);
        return new(path[^1], path, present);
    }

    /// <summary>Records an engine-authorized hop to a registered cell. The caller supplies the expected Thread and visit sequence.</summary>
    public async Task RecordHopAsync(Guid survivorId, WorldPosition destination, long expectedSequence, CancellationToken ct,
        string? communicationNetwork = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(destination.Tick);
        if (string.IsNullOrWhiteSpace(destination.ClockKey) || destination.ClockKey.Length > 80)
            throw new ArgumentException("A world clock key is required.", nameof(destination));
        if (communicationNetwork is { } network && (string.IsNullOrWhiteSpace(network) || network.Length > 80))
            throw new ArgumentException("Use a valid story communication network.", nameof(communicationNetwork));
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await new TimelineStateStore(db).LockAsync(survivorId, destination.TimelineId, ct);
        var last = await LatestAsync(survivorId, destination.TimelineId, ct);
        if (last is null || last.Sequence != expectedSequence)
            throw new TimelineConflictException("The survivor's location changed or has not been recorded.");
        var cellId = await db.Set<WorldCellRecord>().Where(cell => cell.RealmId == destination.Cell.RealmId &&
            cell.Q == destination.Cell.Q && cell.R == destination.Cell.R).Select(cell => (Guid?)cell.Id).SingleOrDefaultAsync(ct)
            ?? throw new ArgumentException("The destination cell is not registered.", nameof(destination));
        await AppendAsync(new() { SurvivorId = survivorId, TimelineId = destination.TimelineId, CellId = cellId,
            ClockKey = destination.ClockKey, Tick = destination.Tick, CommunicationNetwork = communicationNetwork }, last, ct);
        await tx.CommitAsync(ct);
    }

    // Existing session and timeline operations call this while holding the survivor lock.
    internal async Task RecordAuthoredAsync(SessionState state, CancellationToken ct, bool onlyIfMissing = false)
    {
        if (AuthoredPosition(state) is not { } visit)
            return;
        var last = await LatestAsync(state.SurvivorId, state.TimelineId, ct);
        if (last is not null && (onlyIfMissing || !last.Authored || last.Tick > visit.Tick))
            return;
        await AppendAsync(visit, last, ct);
    }

    // A generated scene consumes one story minute. Move the clock without inventing a new location or network.
    internal async Task AdvanceGeneratedAsync(Guid survivorId, Guid timelineId, CancellationToken ct)
    {
        var last = await LatestAsync(survivorId, timelineId, ct);
        if (last is null)
            return;
        await AppendAsync(new() { SurvivorId = survivorId, TimelineId = timelineId, CellId = last.CellId,
            ClockKey = last.ClockKey, Tick = checked(last.Tick + 1), Authored = last.Authored,
            CommunicationNetwork = last.CommunicationNetwork }, last, ct);
    }

    private Task<WorldVisitRecord?> LatestAsync(Guid survivorId, Guid timelineId, CancellationToken ct) =>
        db.Set<WorldVisitRecord>().AsNoTracking().Where(row => row.SurvivorId == survivorId && row.TimelineId == timelineId)
            .OrderByDescending(row => row.Sequence).FirstOrDefaultAsync(ct);

    private async Task AppendAsync(WorldVisitRecord visit, WorldVisitRecord? last, CancellationToken ct)
    {
        if (last?.CellId == visit.CellId && last.ClockKey == visit.ClockKey && last.Tick == visit.Tick &&
            last.CommunicationNetwork == visit.CommunicationNetwork)
            return;
        visit.Sequence = checked((last?.Sequence ?? 0) + 1);
        db.Add(visit);
        await db.SaveChangesAsync(ct);
        db.Entry(visit).State = EntityState.Detached;
    }

    private static WorldVisitRecord? AuthoredPosition(SessionState state)
    {
        if (state.EventKey == DayOneScenes.GeneratedOpeningKey)
        {
            var arrival = state.Arrival ?? DayOneArrival.Original;
            return new() { SurvivorId = state.SurvivorId, TimelineId = state.TimelineId, CellId = arrival.StartCellId,
                ClockKey = SilenceClock, Tick = arrival.Minute, Authored = true };
        }
        var opening = state.Arrival?.Opening() ?? DayOneScenes.Opening;
        if (state.SceneKey != opening.Key || state.SurvivorDay != opening.SurvivorDay)
            return null;
        var beat = opening.Beats.FirstOrDefault(beat => beat.Key == state.BeatKey);
        if (!TimeOnly.TryParseExact(beat?.Time, "h:mm tt", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
            return null;
        var cell = (state.Arrival ?? DayOneArrival.Original).CellId(state.BeatKey, state.LastOutcome);
        return new() { SurvivorId = state.SurvivorId, TimelineId = state.TimelineId, CellId = cell,
            ClockKey = SilenceClock, Tick = time.Hour * 60 + time.Minute, Authored = true };
    }
}
