using System.Text.Json;
using Fatewake.GameEngine;
using Microsoft.EntityFrameworkCore;

namespace Fatewake.Infrastructure.Persistence;

public interface IResolutionStore
{
    Task<ActionResolution> PersistAsync(Guid eventInstanceId, Guid survivorId, Guid timelineId, Guid idempotencyKey, CandidateAction action, ActionResolution resolution, CancellationToken cancellationToken = default);
}

public sealed class ResolutionStore(FatewakeDbContext db) : IResolutionStore
{
    public async Task<ActionResolution> PersistAsync(Guid eventInstanceId, Guid survivorId, Guid timelineId, Guid idempotencyKey, CandidateAction action, ActionResolution resolution, CancellationToken cancellationToken = default)
    {
        var prior = await (from a in db.ActionAttempts
                           join r in db.ActionResolutions on a.Id equals r.ActionAttemptId
                           where a.EventInstanceId == eventInstanceId && a.IdempotencyKey == idempotencyKey
                           select r).SingleOrDefaultAsync(cancellationToken);
        if (prior is not null) return Deserialize(prior);

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var timeline = await db.Timelines.FromSqlInterpolated($"SELECT * FROM timeline WHERE \"Id\" = {timelineId} FOR UPDATE").SingleAsync(cancellationToken);
        var sequence = timeline.NextEventSequence++;

        var attempt = new ActionAttemptRecord { Id = Guid.NewGuid(), IdempotencyKey = idempotencyKey, EventInstanceId = eventInstanceId, SurvivorId = survivorId, InputKind = action.RawInput is null ? "authored" : "freeform", RawInput = action.RawInput, AuthoredActionKey = action.RawInput is null ? action.ActionType : null, CandidateAction = JsonSerializer.Serialize(action), SubmittedAt = now };
        db.ActionAttempts.Add(attempt);
        db.ActionResolutions.Add(new ActionResolutionRecord { Id = Guid.NewGuid(), ActionAttemptId = attempt.Id, OutcomeType = resolution.OutcomeKey, ResolvedAction = JsonSerializer.Serialize(action), AuthoritativeEffects = JsonSerializer.Serialize(resolution.Effects), NarrativeFacts = JsonSerializer.Serialize(resolution.NarrativeFacts), RulesVersion = resolution.RulesVersion, ResolvedAt = now });
        var gameEvent = new GameEventRecord { Id = Guid.NewGuid(), TimelineId = timelineId, SurvivorId = survivorId, EventType = "action_resolved", Sequence = sequence, SurvivorDay = 1, CorrelationId = idempotencyKey, Payload = JsonSerializer.Serialize(new { action, resolution.OutcomeKey, resolution.Effects }), OccurredAt = now };
        db.GameEvents.Add(gameEvent);
        foreach (var wake in resolution.Wakes) db.Wakes.Add(new WakeRecord { Id = Guid.NewGuid(), TimelineId = timelineId, OriginEventId = gameEvent.Id, WakeType = wake.Type, Scope = wake.Scope, TargetEntityType = wake.Target is null ? null : "character", Severity = (short)wake.Severity, State = WakeState.Active, Properties = JsonSerializer.Serialize(wake.Properties), CreatedDay = 1, CreatedAt = now });
        var episode = await db.EventInstances.SingleAsync(x => x.Id == eventInstanceId, cancellationToken);
        string sceneKey = "day1-0617-0643", beatKey = "first-choice";
        if (!string.IsNullOrWhiteSpace(episode.State) && episode.State != "{}")
        {
            using var stateDoc = JsonDocument.Parse(episode.State);
            if (stateDoc.RootElement.TryGetProperty("sceneKey", out var s) && !string.IsNullOrWhiteSpace(s.GetString())) sceneKey = s.GetString()!;
            if (stateDoc.RootElement.TryGetProperty("beatKey", out var b) && !string.IsNullOrWhiteSpace(b.GetString())) beatKey = b.GetString()!;
        }
        episode.State = JsonSerializer.Serialize(new { lastOutcome = resolution.OutcomeKey, narrativeFacts = resolution.NarrativeFacts, sceneKey, beatKey });
        episode.Version++;
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return resolution;
    }

    private static ActionResolution Deserialize(ActionResolutionRecord r) => new(
        true, r.OutcomeType,
        JsonSerializer.Deserialize<List<StateEffect>>(r.AuthoritativeEffects) ?? [],
        [],
        JsonSerializer.Deserialize<Dictionary<string,string>>(r.NarrativeFacts) ?? new(),
        r.RulesVersion);
}
