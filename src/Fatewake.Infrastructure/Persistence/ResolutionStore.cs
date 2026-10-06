using System.Text.Json;
using Fatewake.GameEngine;
using Microsoft.EntityFrameworkCore;

namespace Fatewake.Infrastructure.Persistence;

public interface IResolutionStore
{
    Task PersistAsync(Guid eventInstanceId, Guid survivorId, Guid timelineId, CandidateAction action, ActionResolution resolution, CancellationToken cancellationToken = default);
}

public sealed class ResolutionStore(FatewakeDbContext db) : IResolutionStore
{
    public async Task PersistAsync(Guid eventInstanceId, Guid survivorId, Guid timelineId, CandidateAction action, ActionResolution resolution, CancellationToken cancellationToken = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var attempt = new ActionAttemptRecord { Id = Guid.NewGuid(), EventInstanceId = eventInstanceId, SurvivorId = survivorId, InputKind = action.RawInput is null ? "authored" : "freeform", RawInput = action.RawInput, AuthoredActionKey = action.RawInput is null ? action.ActionType : null, CandidateAction = JsonSerializer.Serialize(action), SubmittedAt = now };
        db.ActionAttempts.Add(attempt);
        db.ActionResolutions.Add(new ActionResolutionRecord { Id = Guid.NewGuid(), ActionAttemptId = attempt.Id, OutcomeType = resolution.OutcomeKey, ResolvedAction = JsonSerializer.Serialize(action), AuthoritativeEffects = JsonSerializer.Serialize(resolution.Effects), NarrativeFacts = JsonSerializer.Serialize(resolution.NarrativeFacts), RulesVersion = resolution.RulesVersion, ResolvedAt = now });
        var gameEvent = new GameEventRecord { Id = Guid.NewGuid(), TimelineId = timelineId, SurvivorId = survivorId, EventType = "action_resolved", Sequence = await NextSequence(timelineId, cancellationToken), SurvivorDay = 1, CorrelationId = Guid.NewGuid(), Payload = JsonSerializer.Serialize(new { action, resolution.OutcomeKey, resolution.Effects }), OccurredAt = now };
        db.GameEvents.Add(gameEvent);
        foreach (var wake in resolution.Wakes) db.Wakes.Add(new WakeRecord { Id = Guid.NewGuid(), TimelineId = timelineId, OriginEventId = gameEvent.Id, WakeType = wake.Type, Scope = wake.Scope, TargetEntityType = wake.Target is null ? null : "character", Severity = (short)wake.Severity, State = "active", Properties = JsonSerializer.Serialize(wake.Properties), CreatedDay = 1, CreatedAt = now });
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
    }

    private async Task<long> NextSequence(Guid timelineId, CancellationToken ct) => (await db.GameEvents.Where(x => x.TimelineId == timelineId).MaxAsync(x => (long?)x.Sequence, ct) ?? 0) + 1;
}
