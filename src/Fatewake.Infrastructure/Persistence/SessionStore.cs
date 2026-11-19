using System.Text.Json;
using Fatewake.Infrastructure.Story;
using Fatewake.Story;
using Fatewake.Observability;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fatewake.Infrastructure.Persistence;

/// <summary>Resumes the selected branch and atomically records reached presentation history and checkpoints.</summary>
/// <see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/SessionStore.md">Documentation</see>
public sealed class SessionStore(FatewakeDbContext db, ILogger<SessionStore>? log = null) : ISessionStore
{
    /// <summary>Resumes only the selected account-owned branch and records reached history/checkpoints in the same transaction.</summary>
    public async Task<SessionState> StartOrResumeAsync(Guid? survivorId, string broadRegion, CancellationToken ct = default, Guid? accountId = null)
    {
        using var operation = OperationTelemetry.Start("session.start_or_resume", log);
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            if (accountId is { } owner)
            {
                var account = await db.Accounts.FromSqlInterpolated($"""SELECT * FROM account WHERE "Id" = {owner} FOR UPDATE""").SingleAsync(ct);
                if (account.Status != AccountStatus.Active)
                    throw new UnauthorizedAccessException("The account is not active.");
            }
            if (survivorId is null && accountId is not null)
                survivorId = await db.Survivors.Where(row => row.AccountId == accountId)
                    .OrderBy(row => row.CreatedAt).Select(row => (Guid?)row.Id).FirstOrDefaultAsync(ct);
            var states = new TimelineStateStore(db);
            SurvivorRecord survivor;
            EventInstanceRecord episode;
            if (survivorId is { } id && await db.Survivors.AnyAsync(row => row.Id == id && row.AccountId == accountId, ct))
            {
                survivor = await states.LockAsync(id, null, ct);
                episode = await db.EventInstances.SingleAsync(row => row.SurvivorId == id &&
                    row.TimelineId == survivor.TimelineId && row.Status == EventInstanceStatus.Active, ct);
                await db.Entry(episode).ReloadAsync(ct);
            }
            else
            {
                var now = DateTimeOffset.UtcNow;
                var realm = await db.Realms.SingleAsync(row => row.Key == "the-silence", ct);
                var timeline = new TimelineRecord
                {
                    Id = Guid.NewGuid(),
                    RealmId = realm.Id,
                    ProgressionState = "personal",
                    ConvergenceState = "isolated",
                    WorldClockPolicy = "activity",
                    CurrentSurvivorDay = 1,
                    CreatedAt = now
                };
                var arrival = DayOneArrival.Allocate();
                survivor = new()
                {
                    Id = Guid.NewGuid(),
                    AccountId = accountId,
                    TimelineId = timeline.Id,
                    DisplayName = Fatewake.Story.BrandIdentity.PlayerName,
                    IdentityMode = "undetermined",
                    BroadRegion = broadRegion,
                    DayOneSiteKey = arrival.SiteKey,
                    DayOneStartMinute = arrival.Minute,
                    SurvivorDay = 1,
                    Status = SurvivorStatus.Active,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                episode = new()
                {
                    Id = Guid.NewGuid(),
                    TimelineId = timeline.Id,
                    SurvivorId = survivor.Id,
                    EventKey = "day-001-injured-stranger",
                    Status = EventInstanceStatus.Active,
                    SurvivorDay = 1,
                    StartedAt = now,
                    State = JsonSerializer.Serialize(new
                    {
                        sceneKey = StartSceneKey,
                        beatKey = StartBeatKey
                    })
                };
                db.Add(timeline);
                db.Add(survivor);
                db.Add(episode);
                await db.SaveChangesAsync(ct);
                await states.LockAsync(survivor.Id, timeline.Id, ct);
            }
            var branch = await states.EnsureAsync(survivor, ct);
            survivor.SurvivorDay = branch.SurvivorDay;
            survivor.Status = branch.Status;
            var state = ToState(survivor, episode);
            await new WorldMapStore(db).RecordAuthoredAsync(state, ct, onlyIfMissing: true);
            await new StoryJournalService(db).RecordProgressAsync(state, ct);
            await states.CaptureAsync(survivor, episode, ct);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return state;
        }
        catch (Exception ex)
        {
            operation.Fail(ex);
            throw;
        }
    }

    /// <summary>Advances a reached beat under the selected-branch lock; invalid progression never creates a checkpoint.</summary>
    public async Task<SessionState?> SavePresentationProgressAsync(Guid survivorId, Guid eventInstanceId, string sceneKey, string beatKey, CancellationToken ct = default)
    {
        using var operation = OperationTelemetry.Start("session.save_progress", log);
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var states = new TimelineStateStore(db);
            var survivor = await states.LockAsync(survivorId, null, ct);
            var episode = await db.EventInstances.SingleOrDefaultAsync(row => row.Id == eventInstanceId &&
                row.SurvivorId == survivorId && row.TimelineId == survivor.TimelineId, ct)
                ?? throw new TimelineConflictException("Your active timeline changed. Reload to continue.");
            await db.Entry(episode).ReloadAsync(ct);
            var state = ReadState(episode.State);
            var scene = Fatewake.Story.DayOneScenes.Opening;
            var target = scene.Beats.ToList().FindIndex(row => row.Key == beatKey);
            var current = scene.Beats.ToList().FindIndex(row => row.Key == state.beat);
            if (sceneKey != scene.Key || target < 0 || target > current + 1 ||
                (target > scene.IndexOf("first-choice") && string.IsNullOrWhiteSpace(state.outcome)))
            {
                log?.LogWarning("Presentation progress rejected because its scene or beat was not reached");
                return null;
            }
            episode.State = JsonSerializer.Serialize(new
            {
                lastOutcome = state.outcome,
                narrativeFacts = state.facts,
                sceneKey,
                beatKey
            });
            episode.Version++;
            await db.SaveChangesAsync(ct);
            var result = ToState(survivor, episode);
            if (target >= current)
                await new WorldMapStore(db).RecordAuthoredAsync(result, ct);
            await new StoryJournalService(db).RecordProgressAsync(result, ct);
            await states.CaptureAsync(survivor, episode, ct);
            await tx.CommitAsync(ct);
            return result;
        }
        catch (TimelineConflictException)
        {
            log?.LogWarning("Presentation progress rejected because the active Thread changed");
            throw;
        }
        catch (Exception ex)
        {
            operation.Fail(ex);
            throw;
        }
    }

    /// <summary>Destructively resets development-only chapter state, refusing to invalidate preserved descendant branches.</summary>
    public async Task<SessionState?> ResetChapterAsync(Guid survivorId, Guid eventInstanceId, CancellationToken ct = default)
    {
        using var operation = OperationTelemetry.Start("session.reset_chapter", log);
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var survivor = await new TimelineStateStore(db).LockAsync(survivorId, null, ct);
            var episode = await db.EventInstances.SingleOrDefaultAsync(row => row.Id == eventInstanceId &&
                row.SurvivorId == survivorId && row.TimelineId == survivor.TimelineId, ct);
            if (episode is null)
            {
                log?.LogWarning("Development chapter reset rejected because the active episode changed");
                return null;
            }
            await db.Entry(episode).ReloadAsync(ct);
            if (await db.Set<TimelineBranchRecord>().AnyAsync(row => row.ParentTimelineId == survivor.TimelineId, ct))
                throw new TimelineConflictException("This chapter has durable checkpoints or branches. Start a fresh Day 1 branch instead.");
            if (await db.Set<WorldVisitRecord>().AnyAsync(row => row.SurvivorId == survivorId &&
                row.TimelineId == survivor.TimelineId && !row.Authored, ct))
                throw new TimelineConflictException("This Thread has recorded hops. Start a fresh Day 1 branch instead.");
            await db.Set<TimelineCheckpointRecord>().Where(row => row.TimelineId == survivor.TimelineId &&
                row.EventInstanceId == eventInstanceId).ExecuteDeleteAsync(ct);
            var attempts = db.ActionAttempts.Where(row => row.EventInstanceId == eventInstanceId && row.SurvivorId == survivorId);
            var events = db.GameEvents.Where(row => row.TimelineId == survivor.TimelineId &&
                attempts.Any(attempt => attempt.IdempotencyKey == row.CorrelationId));
            await db.Set<StoryPanelRecord>().Where(row => row.EventInstanceId == eventInstanceId ||
                events.Any(source => source.Id == row.SourceEventId)).ExecuteDeleteAsync(ct);
            await db.Wakes.Where(row => events.Any(source => source.Id == row.OriginEventId)).ExecuteDeleteAsync(ct);
            await events.ExecuteDeleteAsync(ct);
            await db.ActionResolutions.Where(row => attempts.Any(attempt => attempt.Id == row.ActionAttemptId)).ExecuteDeleteAsync(ct);
            await attempts.ExecuteDeleteAsync(ct);
            var branch = await db.Set<SurvivorTimelineStateRecord>().SingleAsync(row => row.SurvivorId == survivorId &&
                row.TimelineId == survivor.TimelineId, ct);
            branch.Facts = "{}";
            branch.Version++;
            episode.State = JsonSerializer.Serialize(new
            {
                sceneKey = StartSceneKey,
                beatKey = StartBeatKey
            });
            episode.Version++;
            await db.SaveChangesAsync(ct);
            await db.Set<WorldVisitRecord>().Where(row => row.SurvivorId == survivorId && row.TimelineId == survivor.TimelineId)
                .ExecuteDeleteAsync(ct);
            var reset = ToState(survivor, episode);
            await new WorldMapStore(db).RecordAuthoredAsync(reset, ct);
            await new StoryJournalService(db).RecordProgressAsync(reset, ct);
            await new TimelineStateStore(db).CaptureAsync(survivor, episode, ct);
            await tx.CommitAsync(ct);
            log?.LogInformation("Development chapter reset completed for survivor {SurvivorId}", survivorId);
            return ToState(survivor, episode);
        }
        catch (TimelineConflictException)
        {
            log?.LogWarning("Development chapter reset rejected because preserved Thread history would be invalidated");
            throw;
        }
        catch (Exception ex)
        {
            operation.Fail(ex);
            throw;
        }
    }

    private const string StartSceneKey = "day1-0617-0643";
    private const string StartBeatKey = "phone-0617";

    internal static SessionState ToState(SurvivorRecord survivor, EventInstanceRecord episode)
    {
        var state = ReadState(episode.State);
        return new(survivor.Id, episode.TimelineId, episode.Id, episode.SurvivorDay, episode.EventKey,
            episode.Status.ToString().ToLowerInvariant(), state.outcome, state.facts, state.scene, state.beat, episode.Version, Arrival(survivor));
    }

    /// <summary>Reads a saved arrival, with null retaining the original opening for legacy saves.</summary>
    public static DayOneArrival? Arrival(SurvivorRecord survivor) =>
        survivor.DayOneSiteKey is { } site && survivor.DayOneStartMinute is { } minute ? new(site, minute) : null;

    private static (string? outcome, IReadOnlyDictionary<string, string> facts, string scene, string beat) ReadState(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        return (
            root.TryGetProperty("lastOutcome", out var outcome) ? outcome.GetString() : null,
            root.TryGetProperty("narrativeFacts", out var facts)
                ? facts.Deserialize<Dictionary<string, string>>() ?? [] : new Dictionary<string, string>(),
            root.TryGetProperty("sceneKey", out var scene) ? scene.GetString() ?? StartSceneKey : StartSceneKey,
            root.TryGetProperty("beatKey", out var beat) ? beat.GetString() ?? StartBeatKey : StartBeatKey);
    }
}
