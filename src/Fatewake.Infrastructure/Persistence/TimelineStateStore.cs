using System.Text.Json;
using Fatewake.GameEngine;
using Fatewake.Observability;
using Fatewake.Story;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fatewake.Infrastructure.Persistence;

/// <summary>Loads canonical branch facts and captures reached checkpoints under the survivor/timeline write lock.</summary>
/// <see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/TimelineStateStore.md">Documentation</see>
public sealed class TimelineStateStore(FatewakeDbContext db, ILogger<TimelineStateStore>? log = null)
{
    /// <summary>Requires an existing transaction and locks/reloads survivor before timeline, rejecting stale selected-branch context.</summary>
    public async Task<SurvivorRecord> LockAsync(Guid survivorId, Guid? expectedTimeline, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Timeline writes require a transaction.");
        var survivor = await db.Survivors
            .FromSqlInterpolated($"""SELECT * FROM survivor WHERE "Id" = {survivorId} FOR UPDATE""")
            .SingleOrDefaultAsync(ct) ?? throw new TimelineConflictException("The survivor is unavailable.");
        await db.Entry(survivor).ReloadAsync(ct);
        if (expectedTimeline is { } expected && survivor.TimelineId != expected)
            throw new TimelineConflictException("Your active timeline changed in another tab. Reload to continue.");
        var timeline = await db.Timelines
            .FromSqlInterpolated($"""SELECT * FROM timeline WHERE "Id" = {survivor.TimelineId} FOR UPDATE""")
            .SingleAsync(ct);
        await db.Entry(timeline).ReloadAsync(ct);
        await EnsureAsync(survivor, ct);
        return survivor;
    }

    /// <summary>Initializes only a legacy current-state projection from authoritative effects, never an invented historical checkpoint.</summary>
    public async Task<SurvivorTimelineStateRecord> EnsureAsync(SurvivorRecord survivor, CancellationToken ct)
    {
        var state = await db.Set<SurvivorTimelineStateRecord>()
            .SingleOrDefaultAsync(row => row.SurvivorId == survivor.Id && row.TimelineId == survivor.TimelineId, ct);
        if (state is null)
        {
            var facts = new Dictionary<string, string>(StringComparer.Ordinal);
            var effects = await (from attempt in db.ActionAttempts
                                 join result in db.ActionResolutions on attempt.Id equals result.ActionAttemptId
                                 join episode in db.EventInstances on attempt.EventInstanceId equals episode.Id
                                 where attempt.SurvivorId == survivor.Id && episode.TimelineId == survivor.TimelineId
                                 orderby result.ResolvedAt, result.Id
                                 select result.AuthoritativeEffects).ToArrayAsync(ct);
            foreach (var json in effects)
                foreach (var effect in JsonSerializer.Deserialize<StateEffect[]>(json) ?? [])
                    facts[effect.Key] = effect.Value;
            state = new()
            {
                SurvivorId = survivor.Id,
                TimelineId = survivor.TimelineId,
                SurvivorDay = survivor.SurvivorDay,
                Status = survivor.Status,
                Facts = JsonSerializer.Serialize(facts)
            };
            db.Add(state);
        }
        else if (db.Entry(state).State == EntityState.Unchanged)
            await db.Entry(state).ReloadAsync(ct);
        if (!await db.Set<TimelineBranchRecord>().AnyAsync(row => row.TimelineId == survivor.TimelineId, ct) &&
            await db.Survivors.CountAsync(row => row.TimelineId == survivor.TimelineId, ct) == 1)
        {
            db.Add(new TimelineBranchRecord
            {
                TimelineId = survivor.TimelineId,
                SurvivorId = survivor.Id,
                RootTimelineId = survivor.TimelineId,
                CreatedAt = survivor.CreatedAt
            });
        }
        await db.SaveChangesAsync(ct);
        return state;
    }

    /// <summary>Loads the active event's canonical facts under the shared lock rather than trusting browser or narrative state.</summary>
    public async Task<GameSnapshot> LoadAsync(Guid survivorId, Guid timelineId, Guid eventId, CancellationToken ct)
    {
        await using var tx = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(ct) : null;
        var survivor = await LockAsync(survivorId, timelineId, ct);
        var episode = await db.EventInstances.AsNoTracking()
            .SingleAsync(row => row.Id == eventId && row.SurvivorId == survivorId && row.TimelineId == timelineId, ct);
        var state = await EnsureAsync(survivor, ct);
        var result = new GameSnapshot(survivorId, timelineId, state.SurvivorDay, episode.EventKey,
            JsonSerializer.Deserialize<Dictionary<string, string>>(state.Facts) ?? []);
        if (tx is not null)
            await tx.CommitAsync(ct);
        return result;
    }

    /// <summary>Excludes shared, externally disclosed or cross-timeline-dependent history that personal branching cannot undo.</summary>
    public async Task<bool> EligibleAsync(Guid timelineId, CancellationToken ct)
    {
        return await db.Timelines.AnyAsync(row => row.Id == timelineId && row.ProgressionState == "personal" &&
                row.ConvergenceState == "isolated" && row.WorldClockPolicy == "activity", ct) &&
            await db.Survivors.CountAsync(row => row.TimelineId == timelineId, ct) <= 1 &&
            await db.Set<SurvivorTimelineStateRecord>().CountAsync(row => row.TimelineId == timelineId, ct) <= 1 &&
            !await db.Wakes.AnyAsync(row => row.TimelineId == timelineId && row.Scope != "Personal", ct) &&
            !await db.Set<StoryPanelRecord>().AnyAsync(row => row.TimelineId == timelineId && row.SourceEventId != null, ct) &&
            !await db.GameEvents.AnyAsync(row => row.TimelineId == timelineId && row.CausationEventId != null &&
                db.GameEvents.Any(source => source.Id == row.CausationEventId && source.TimelineId != timelineId), ct);
    }

    /// <summary>Freezes a supported reached opening/decision once within its caller's locked transaction, preserving prior disclosure and pins.</summary>
    public async Task CaptureAsync(SurvivorRecord survivor, EventInstanceRecord episode, CancellationToken ct)
    {
        if (survivor.AccountId is null || episode.SurvivorDay != 1 || episode.EventKey != "day-001-injured-stranger" ||
            !await EligibleAsync(episode.TimelineId, ct))
            return;
        using var operation = OperationTelemetry.Start("timeline.checkpoint", log);
        using var state = JsonDocument.Parse(episode.State);
        var beatKey = state.RootElement.TryGetProperty("beatKey", out var beat) ? beat.GetString() : null;
        var hasOutcome = state.RootElement.TryGetProperty("lastOutcome", out var outcome) &&
            !string.IsNullOrWhiteSpace(outcome.GetString());
        if (hasOutcome || beatKey is not ("phone-0617" or "first-choice"))
            return;
        if (await db.Set<TimelineCheckpointRecord>().AnyAsync(row => row.TimelineId == episode.TimelineId &&
            row.EventInstanceId == episode.Id && row.Key == beatKey, ct))
            return;
        var branchState = await EnsureAsync(survivor, ct);
        var snapshot = new TimelineSnapshot(1, TimelineSnapshotCodec.RulesVersion, TimelineSnapshotCodec.ContentVersion,
            branchState.SurvivorDay, branchState.Status, branchState.Facts, episode,
            await db.Wakes.AsNoTracking().Where(row => row.TimelineId == episode.TimelineId).OrderBy(row => row.Id).ToArrayAsync(ct),
            await db.Set<StoryPanelRecord>().AsNoTracking().Where(row => row.TimelineId == episode.TimelineId &&
                row.SurvivorId == survivor.Id).OrderBy(row => row.Sequence).ToArrayAsync(ct),
            await db.SurvivorVisualIdentities.AsNoTracking().SingleOrDefaultAsync(row => row.SurvivorId == survivor.Id, ct));
        var json = JsonSerializer.Serialize(snapshot);
        db.Add(new TimelineCheckpointRecord
        {
            Id = Guid.NewGuid(),
            TimelineId = episode.TimelineId,
            SurvivorId = survivor.Id,
            EventInstanceId = episode.Id,
            Key = beatKey,
            Label = beatKey == "phone-0617" ? "The Silence - opening" : "Before Michelle's plea",
            TimeLabel = "Day 1, " + (SessionStore.Arrival(survivor)?.Opening() ?? DayOneScenes.Opening).Beat(beatKey).Time,
            Snapshot = json,
            SnapshotHash = TimelineSnapshotCodec.Hash(json),
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(ct);
    }
}
