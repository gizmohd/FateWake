using System.Text.Json;
using Fatewake.GameEngine;
using Fatewake.Infrastructure.Authentication;
using Fatewake.Infrastructure.Persistence;
using Fatewake.Observability;
using Fatewake.Story;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fatewake.Infrastructure.Story;

/// <summary>Default <see cref="IStoryPlayService"/> over <c>generated_scene</c> and the scene scheduler.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Story/StoryPlayService.md">Documentation</see>.</remarks>
public sealed class StoryPlayService(FatewakeDbContext db, ISceneScheduler scheduler, ILogger<StoryPlayService> log)
    : IStoryPlayService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    /// <inheritdoc />
    public async Task<StorySceneView> GetCurrentAsync(Guid survivorId, bool retryFailed, CancellationToken ct)
    {
        using var operation = OperationTelemetry.Start("story.scene.current", log);
        try
        {
            var survivor = await db.Survivors.SingleAsync(x => x.Id == survivorId, ct);
            var scenes = db.Set<GeneratedSceneRecord>().Where(x => x.SurvivorId == survivorId && x.TimelineId == survivor.TimelineId);
            var sequence = await scenes.Where(x => x.CompletedAt == null).Select(x => (int?)x.Sequence).MinAsync(ct)
                ?? (await scenes.Select(x => (int?)x.Sequence).MaxAsync(ct) ?? 0) + 1;
            var scene = await scheduler.EnsureAsync(survivorId, survivor.TimelineId, sequence, retryFailed, ct);
            if (scene.Status == GeneratedSceneStatus.Ready && !scene.HasDecision)
                await scheduler.EnsureAsync(survivorId, survivor.TimelineId, sequence + 1, false, ct);
            var level = (await AccountProfileService.GetContentAsync(db, survivor.AccountId, ct)).Level;
            return ToView(scene, survivor.SurvivorDay, level);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            operation.Fail(ex);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<StorySceneView> AdvanceAsync(Guid survivorId, int sequence, CancellationToken ct)
    {
        var (scene, level) = await CurrentReadyAsync(survivorId, sequence, ct);
        if (VisibleSpec(scene, level)?.HasDecision == true)
            throw new InvalidOperationException("This scene requires a decision.");
        await CompleteAsync(scene, null, null, ct);
        return await GetCurrentAsync(survivorId, false, ct);
    }

    /// <inheritdoc />
    public async Task<StorySceneView?> ChooseAsync(Guid survivorId, int sequence, string actionType, string? rawInput,
        CancellationToken ct)
    {
        var (scene, level) = await CurrentReadyAsync(survivorId, sequence, ct);
        var spec = VisibleSpec(scene, level);
        if (spec?.HasDecision != true)
            throw new InvalidOperationException("This scene has no decision.");
        var result = SceneChoiceResolver.Resolve(spec.Choices.Select(x => x.ActionType).ToList(),
            spec.Choices.Any(x => x.AllowFreeform), actionType, rawInput);
        if (!result.Accepted || !StoryContentPolicy.Allows(level, result.FreeformText))
            return null;
        await CompleteAsync(scene, result.ActionType, result.FreeformText, ct);
        log.LogInformation("Recorded decision for scene {SceneSequence}", sequence);
        return await GetCurrentAsync(survivorId, false, ct);
    }

    private async Task<(GeneratedSceneRecord Scene, ContentLevel Level)> CurrentReadyAsync(Guid survivorId, int sequence, CancellationToken ct)
    {
        var survivor = await db.Survivors.SingleAsync(x => x.Id == survivorId, ct);
        var scene = await db.Set<GeneratedSceneRecord>().AsNoTracking().SingleOrDefaultAsync(x => x.SurvivorId == survivorId &&
            x.TimelineId == survivor.TimelineId && x.Sequence == sequence, ct);
        if (scene is null || scene.Status != GeneratedSceneStatus.Ready || scene.CompletedAt is not null ||
            await db.Set<GeneratedSceneRecord>().AnyAsync(x => x.SurvivorId == survivorId &&
                x.TimelineId == survivor.TimelineId && x.Sequence < sequence && x.CompletedAt == null, ct))
            throw new InvalidOperationException("That scene is not the current ready scene.");
        var level = (await AccountProfileService.GetContentAsync(db, survivor.AccountId, ct)).Level;
        return (scene, level);
    }

    private async Task CompleteAsync(GeneratedSceneRecord scene, string? action, string? text, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await new TimelineStateStore(db).LockAsync(scene.SurvivorId, scene.TimelineId, ct);
        var updated = await db.Set<GeneratedSceneRecord>()
            .Where(x => x.Id == scene.Id && x.CompletedAt == null && x.Status == GeneratedSceneStatus.Ready)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.CompletedAt, DateTimeOffset.UtcNow)
                .SetProperty(x => x.ChosenActionType, action).SetProperty(x => x.ChosenText, text), ct);
        if (updated != 1)
            throw new InvalidOperationException("This scene has already been completed.");
        var openingVersion = await db.EventInstances.Where(episode => episode.SurvivorId == scene.SurvivorId &&
                episode.TimelineId == scene.TimelineId && episode.EventKey == DayOneScenes.GeneratedOpeningKey)
            .ExecuteUpdateAsync(update => update.SetProperty(episode => episode.Version, episode => episode.Version + 1), ct);
        if (openingVersion > 0)
            await new WorldMapStore(db).AdvanceGeneratedAsync(scene.SurvivorId, scene.TimelineId, ct);
        await transaction.CommitAsync(ct);
    }

    internal static SceneSpec? VisibleSpec(GeneratedSceneRecord scene, ContentLevel level)
    {
        if (scene.ContentLevel is not { } recordedLevel || recordedLevel > level || scene.Spec is null)
            return null;
        var spec = JsonSerializer.Deserialize<SceneSpec>(scene.Spec, Json);
        return spec is not null && StoryContentPolicy.AllowsScene(level, spec) ? spec : null;
    }

    private static StorySceneView ToView(GeneratedSceneRecord scene, int day, ContentLevel level)
    {
        if (scene.Status != GeneratedSceneStatus.Ready)
            return new(scene.Status, scene.Sequence, day, null, null);
        var visible = VisibleSpec(scene, level);
        return new(visible is null ? "Filtered" : scene.Status, scene.Sequence, day, null, visible);
    }
}
