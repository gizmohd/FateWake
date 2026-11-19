using System.Text.Json;
using Fatewake.GameEngine;
using Fatewake.Infrastructure.Story;
using Fatewake.Observability;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fatewake.Infrastructure.Persistence;

/// <summary>Commits one decision, its pre-decision checkpoint, effects and audience history under a branch lock.</summary>
/// <see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/ResolutionStore.md">Documentation</see>
public sealed class ResolutionStore(FatewakeDbContext db, ILogger<ResolutionStore>? log = null) : IResolutionStore
{
    /// <summary>Atomically persists accepted effects once; matching retries return the original result and conflicting decisions are rejected.</summary>
    public async Task<ActionResolution> PersistAsync(Guid eventInstanceId, Guid survivorId, Guid timelineId,
        Guid idempotencyKey, CandidateAction action, ActionResolution resolution, CancellationToken cancellationToken = default)
    {
        using var operation = OperationTelemetry.Start("game.persist_resolution", log);
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
            var states = new TimelineStateStore(db);
            var survivor = await states.LockAsync(survivorId, timelineId, cancellationToken);
            var episode = await db.EventInstances.SingleOrDefaultAsync(row => row.Id == eventInstanceId &&
                row.SurvivorId == survivorId && row.TimelineId == timelineId, cancellationToken)
                ?? throw new TimelineConflictException("The decision does not belong to your active timeline.");
            await db.Entry(episode).ReloadAsync(cancellationToken);
            var prior = await (from recordedAttempt in db.ActionAttempts
                               join recordedResult in db.ActionResolutions on recordedAttempt.Id equals recordedResult.ActionAttemptId
                               where recordedAttempt.EventInstanceId == eventInstanceId && recordedAttempt.IdempotencyKey == idempotencyKey
                               select new
                               {
                                   recordedAttempt.CandidateAction,
                                   Result = recordedResult
                               }).SingleOrDefaultAsync(cancellationToken);
            if (prior is not null)
            {
                var original = JsonSerializer.Deserialize<CandidateAction>(prior.CandidateAction)
                    ?? throw new InvalidDataException("A recorded action is invalid.");
                if (original.ActionType != action.ActionType || original.RawInput != action.RawInput ||
                    original.Arguments.Count != action.Arguments.Count ||
                    original.Arguments.Any(pair => !action.Arguments.TryGetValue(pair.Key, out var value) || value != pair.Value))
                    throw new TimelineConflictException("This request identifier belongs to a different action.");
                var payload = await db.GameEvents.Where(row => row.TimelineId == timelineId && row.CorrelationId == idempotencyKey)
                    .Select(row => row.Payload).SingleAsync(cancellationToken);
                using var document = JsonDocument.Parse(payload);
                var result = document.RootElement.TryGetProperty("resolution", out var recorded)
                    ? recorded.Deserialize<ActionResolution>() ?? throw new InvalidDataException("Invalid recorded resolution.")
                    : new ActionResolution(true, prior.Result.OutcomeType,
                        JsonSerializer.Deserialize<List<StateEffect>>(prior.Result.AuthoritativeEffects) ?? [],
                        [], JsonSerializer.Deserialize<Dictionary<string, string>>(prior.Result.NarrativeFacts) ?? [], prior.Result.RulesVersion);
                await tx.CommitAsync(cancellationToken);
                return result;
            }
            using (var state = JsonDocument.Parse(episode.State))
            {
                if (state.RootElement.TryGetProperty("lastOutcome", out var outcome) && !string.IsNullOrWhiteSpace(outcome.GetString()))
                    throw new TimelineConflictException("This decision is already resolved. Create a branch to choose differently.");
            }
            if (!resolution.Accepted || resolution.RulesVersion != TimelineSnapshotCodec.RulesVersion)
                throw new TimelineConflictException("Only a supported accepted decision can be persisted.");
            episode.State = """{"sceneKey":"day1-0617-0643","beatKey":"first-choice"}""";
            await new StoryJournalService(db).RecordProgressAsync(SessionStore.ToState(survivor, episode), cancellationToken);
            await states.CaptureAsync(survivor, episode, cancellationToken);
            var now = DateTimeOffset.UtcNow;
            var timeline = await db.Timelines.SingleAsync(row => row.Id == timelineId, cancellationToken);
            var attempt = new ActionAttemptRecord
            {
                Id = Guid.NewGuid(),
                IdempotencyKey = idempotencyKey,
                EventInstanceId = eventInstanceId,
                SurvivorId = survivorId,
                InputKind = action.RawInput is null ? "authored" : "freeform",
                RawInput = action.RawInput,
                AuthoredActionKey = action.RawInput is null ? action.ActionType : null,
                CandidateAction = JsonSerializer.Serialize(action),
                SubmittedAt = now
            };
            db.Add(attempt);
            db.Add(new ActionResolutionRecord
            {
                Id = Guid.NewGuid(),
                ActionAttemptId = attempt.Id,
                OutcomeType = resolution.OutcomeKey,
                ResolvedAction = JsonSerializer.Serialize(action),
                AuthoritativeEffects = JsonSerializer.Serialize(resolution.Effects),
                NarrativeFacts = JsonSerializer.Serialize(resolution.NarrativeFacts),
                RulesVersion = resolution.RulesVersion,
                ResolvedAt = now
            });
            var gameEvent = new GameEventRecord
            {
                Id = Guid.NewGuid(),
                TimelineId = timelineId,
                SurvivorId = survivorId,
                EventType = "action_resolved",
                Sequence = timeline.NextEventSequence++,
                SurvivorDay = episode.SurvivorDay,
                CorrelationId = idempotencyKey,
                Payload = JsonSerializer.Serialize(new { action, resolution.OutcomeKey, resolution.Effects, resolution }),
                OccurredAt = now
            };
            db.Add(gameEvent);
            foreach (var wake in resolution.Wakes)
                db.Add(new WakeRecord
                {
                    Id = Guid.NewGuid(),
                    TimelineId = timelineId,
                    OriginEventId = gameEvent.Id,
                    WakeType = wake.Type,
                    Scope = wake.Scope,
                    TargetEntityType = wake.Target is null ? null : "character",
                    Severity = (short)wake.Severity,
                    State = WakeState.Active,
                    Properties = JsonSerializer.Serialize(wake.Properties),
                    CreatedDay = episode.SurvivorDay,
                    CreatedAt = now
                });
            var branchState = await states.EnsureAsync(survivor, cancellationToken);
            var facts = JsonSerializer.Deserialize<Dictionary<string, string>>(branchState.Facts) ?? [];
            foreach (var effect in resolution.Effects)
                facts[effect.Key] = effect.Value;
            branchState.Facts = JsonSerializer.Serialize(facts);
            branchState.Version++;
            episode.State = JsonSerializer.Serialize(new
            {
                lastOutcome = resolution.OutcomeKey,
                narrativeFacts = resolution.NarrativeFacts,
                sceneKey = "day1-0617-0643",
                beatKey = "choice-wake"
            });
            episode.Version++;
            await db.SaveChangesAsync(cancellationToken);
            var reached = SessionStore.ToState(survivor, episode);
            await new WorldMapStore(db).RecordAuthoredAsync(reached, cancellationToken);
            await new StoryJournalService(db).RecordOutcomeAsync(reached, cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return resolution;
        }
        catch (TimelineConflictException)
        {
            log?.LogWarning("Decision persistence rejected because its request or active Thread changed");
            throw;
        }
        catch (Exception ex)
        {
            operation.Fail(ex);
            throw;
        }
    }
}
