using System.Text.Json;
using Fatewake.Observability;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fatewake.Infrastructure.Persistence;

/// <summary>Forks or selects personal branches atomically while retaining stable account-owned survivor identity.</summary>
/// <see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/TimelineBranchService.md">Documentation</see>
public sealed class TimelineBranchService(FatewakeDbContext db, ILogger<TimelineBranchService> log)
{
    /// <summary>Creates and selects an owner-only branch atomically; a matching retry reuses its branch without replaying ancestor effects.</summary>
    public async Task<SessionState> ForkAsync(Guid accountId, Guid survivorId, Guid expectedTimelineId,
        Guid? checkpointId, Guid requestId, CancellationToken ct = default, long? expectedVersion = null)
    {
        using var operation = OperationTelemetry.Start("timeline.fork", log);
        try
        {
            if (requestId == Guid.Empty)
                throw new TimelineConflictException("A branch request identifier is required.");
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await LockAccountAsync(accountId, ct);
            var states = new TimelineStateStore(db);
            var survivor = await states.LockAsync(survivorId, null, ct);
            if (survivor.AccountId != accountId)
                throw new UnauthorizedAccessException();
            var prior = await db.Set<TimelineBranchRecord>()
                .SingleOrDefaultAsync(row => row.SurvivorId == survivorId && row.RequestId == requestId, ct);
            if (prior is not null)
            {
                if (prior.CheckpointId != checkpointId || prior.ParentTimelineId != expectedTimelineId)
                    throw new TimelineConflictException("This request identifier was used for a different branch.");
                if (survivor.TimelineId != prior.TimelineId)
                    throw new TimelineConflictException("That branch was already created. Select it from your timelines.");
                var existing = await CurrentAsync(survivor, ct);
                await tx.CommitAsync(ct);
                return existing;
            }
            if (survivor.TimelineId != expectedTimelineId)
                throw new TimelineConflictException("Your active timeline changed. Reload before branching.");
            await ValidateVersionAsync(survivor, expectedVersion, ct);
            if (!await states.EligibleAsync(expectedTimelineId, ct))
                throw new TimelineConflictException("Only isolated personal timelines without shared-world dependencies can branch.");
            var parent = await db.Set<TimelineBranchRecord>().SingleAsync(row => row.TimelineId == expectedTimelineId, ct);
            TimelineSnapshot snapshot;
            if (checkpointId is { } id)
            {
                var checkpoint = await db.Set<TimelineCheckpointRecord>().SingleOrDefaultAsync(row => row.Id == id &&
                    row.SurvivorId == survivorId && row.TimelineId == expectedTimelineId, ct)
                    ?? throw new TimelineConflictException("Select a checkpoint reached on the current branch.");
                snapshot = TimelineSnapshotCodec.Read(checkpoint.Snapshot, checkpoint.SnapshotHash);
                if (snapshot.Episode.Id != checkpoint.EventInstanceId || snapshot.Episode.TimelineId != expectedTimelineId ||
                    snapshot.Episode.SurvivorId != survivorId)
                    throw new TimelineConflictException("The checkpoint does not match its recorded branch.");
            }
            else
            {
                var visual = await db.SurvivorVisualIdentities.AsNoTracking().SingleOrDefaultAsync(row => row.SurvivorId == survivorId, ct);
                snapshot = new(1, TimelineSnapshotCodec.RulesVersion, TimelineSnapshotCodec.ContentVersion, 1,
                    SurvivorStatus.Active, "{}", Opening(survivorId, expectedTimelineId), [], [], visual);
            }
            var now = DateTimeOffset.UtcNow;
            var timeline = new TimelineRecord
            {
                Id = Guid.NewGuid(),
                RealmId = (await db.Timelines.SingleAsync(row => row.Id == expectedTimelineId, ct)).RealmId,
                ProgressionState = "personal",
                ConvergenceState = "isolated",
                WorldClockPolicy = "activity",
                CurrentSurvivorDay = snapshot.SurvivorDay,
                CreatedAt = now
            };
            var sourceEpisode = snapshot.Episode.Id;
            var episode = snapshot.Episode;
            episode.Id = Guid.NewGuid();
            episode.TimelineId = timeline.Id;
            episode.SurvivorId = survivorId;
            episode.Version = 0;
            episode.StartedAt = now;
            db.Add(timeline);
            db.Add(episode);
            db.Add(new SurvivorTimelineStateRecord
            {
                SurvivorId = survivorId,
                TimelineId = timeline.Id,
                SurvivorDay = snapshot.SurvivorDay,
                Status = snapshot.Status,
                Facts = snapshot.Facts
            });
            db.Add(new TimelineBranchRecord
            {
                TimelineId = timeline.Id,
                SurvivorId = survivorId,
                RootTimelineId = parent.RootTimelineId,
                ParentTimelineId = expectedTimelineId,
                CheckpointId = checkpointId,
                RequestId = requestId,
                Origin = checkpointId is null ? "fresh-day1" : "checkpoint",
                CreatedAt = now
            });
            foreach (var panel in snapshot.Panels)
            {
                panel.Id = Guid.NewGuid();
                panel.TimelineId = timeline.Id;
                if (panel.EventInstanceId == sourceEpisode)
                {
                    panel.EventInstanceId = episode.Id;
                    panel.EntryKey = panel.EntryKey.Replace(sourceEpisode.ToString(), episode.Id.ToString(), StringComparison.Ordinal);
                }
                db.Add(panel);
            }
            foreach (var wake in snapshot.Wakes)
            {
                wake.Id = Guid.NewGuid();
                wake.TimelineId = timeline.Id;
                db.Add(wake);
            }
            survivor.TimelineId = timeline.Id;
            survivor.SurvivorDay = snapshot.SurvivorDay;
            survivor.Status = snapshot.Status;
            survivor.UpdatedAt = now;
            await db.SaveChangesAsync(ct);
            await new Story.StoryJournalService(db).RecordProgressAsync(SessionStore.ToState(survivor, episode), ct);
            await states.CaptureAsync(survivor, episode, ct);
            await tx.CommitAsync(ct);
            log.LogInformation("Personal timeline branch created for survivor {SurvivorId} in timeline {TimelineId}", survivorId, timeline.Id);
            return SessionStore.ToState(survivor, episode);
        }
        catch (Exception ex) when (ex is TimelineConflictException or UnauthorizedAccessException)
        {
            log.LogWarning("Personal Thread creation was rejected because ownership or state changed");
            throw;
        }
        catch (Exception ex)
        {
            operation.Fail(ex);
            throw;
        }
    }

    /// <summary>Selects a preserved personal branch after checking active context/version, keeping both branches' progress intact.</summary>
    public async Task<SessionState> SelectAsync(Guid accountId, Guid survivorId, Guid expectedTimelineId,
        Guid targetTimelineId, CancellationToken ct = default, long? expectedVersion = null)
    {
        using var operation = OperationTelemetry.Start("timeline.switch", log);
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await LockAccountAsync(accountId, ct);
            var states = new TimelineStateStore(db);
            var survivor = await states.LockAsync(survivorId, expectedTimelineId, ct);
            if (survivor.AccountId != accountId)
                throw new UnauthorizedAccessException();
            await ValidateVersionAsync(survivor, expectedVersion, ct);
            if (!await db.Set<TimelineBranchRecord>().AnyAsync(row => row.SurvivorId == survivorId && row.TimelineId == targetTimelineId, ct))
                throw new TimelineConflictException("This branch is unavailable for your survivor.");
            if (!await states.EligibleAsync(expectedTimelineId, ct) || !await states.EligibleAsync(targetTimelineId, ct))
                throw new TimelineConflictException("Shared-world timelines cannot be switched.");
            var target = await db.Set<SurvivorTimelineStateRecord>()
                .SingleAsync(row => row.SurvivorId == survivorId && row.TimelineId == targetTimelineId, ct);
            survivor.TimelineId = targetTimelineId;
            survivor.SurvivorDay = target.SurvivorDay;
            survivor.Status = target.Status;
            survivor.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            var result = await CurrentAsync(survivor, ct);
            await tx.CommitAsync(ct);
            log.LogInformation("Personal timeline selected for survivor {SurvivorId} in timeline {TimelineId}", survivorId, targetTimelineId);
            return result;
        }
        catch (Exception ex) when (ex is TimelineConflictException or UnauthorizedAccessException)
        {
            log.LogWarning("Personal Thread selection was rejected because ownership or state changed");
            throw;
        }
        catch (Exception ex)
        {
            operation.Fail(ex);
            throw;
        }
    }

    private async Task<SessionState> CurrentAsync(SurvivorRecord survivor, CancellationToken ct) =>
        SessionStore.ToState(survivor, await db.EventInstances.AsNoTracking().SingleAsync(row => row.SurvivorId == survivor.Id &&
            row.TimelineId == survivor.TimelineId && row.Status == EventInstanceStatus.Active, ct));

    private async Task LockAccountAsync(Guid accountId, CancellationToken ct)
    {
        var account = await db.Accounts.FromSqlInterpolated($"""SELECT * FROM account WHERE "Id" = {accountId} FOR UPDATE""")
            .AsNoTracking().SingleOrDefaultAsync(ct);
        if (account?.Status != AccountStatus.Active)
            throw new TimelineConflictException("An active account is required to change timelines.");
    }

    private async Task ValidateVersionAsync(SurvivorRecord survivor, long? expectedVersion, CancellationToken ct)
    {
        if (expectedVersion is { } version && (await CurrentAsync(survivor, ct)).Version != version)
            throw new TimelineConflictException("Your story progressed while this page was open. Refresh before changing timelines.");
    }

    private static EventInstanceRecord Opening(Guid survivorId, Guid timelineId) => new()
    {
        Id = Guid.NewGuid(),
        SurvivorId = survivorId,
        TimelineId = timelineId,
        EventKey = "day-001-injured-stranger",
        SurvivorDay = 1,
        Status = EventInstanceStatus.Active,
        State = """{"sceneKey":"day1-0617-0643","beatKey":"phone-0617"}"""
    };
}
