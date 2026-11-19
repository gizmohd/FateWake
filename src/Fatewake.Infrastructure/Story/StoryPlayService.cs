using System.Text.Json;
using Fatewake.GameEngine;
using Fatewake.Infrastructure.Persistence;
using Fatewake.Observability;
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
            return ToView(scene, survivor.SurvivorDay);
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
        var scene = await CurrentReadyAsync(survivorId, sequence, ct);
        if (scene.HasDecision)
            throw new InvalidOperationException("This scene requires a decision.");
        scene.CompletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetCurrentAsync(survivorId, false, ct);
    }

    /// <inheritdoc />
    public async Task<StorySceneView?> ChooseAsync(Guid survivorId, int sequence, string actionType, string? rawInput,
        CancellationToken ct)
    {
        var scene = await CurrentReadyAsync(survivorId, sequence, ct);
        var spec = Spec(scene)!;
        if (!scene.HasDecision)
            throw new InvalidOperationException("This scene has no decision.");
        var result = SceneChoiceResolver.Resolve(spec.Choices.Select(x => x.ActionType).ToList(),
            spec.Choices.Any(x => x.AllowFreeform), actionType, rawInput);
        if (!result.Accepted)
            return null;
        scene.ChosenActionType = result.ActionType;
        scene.ChosenText = result.FreeformText;
        scene.CompletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        log.LogInformation("Recorded decision for scene {SceneSequence}", sequence);
        return await GetCurrentAsync(survivorId, false, ct);
    }

    private async Task<GeneratedSceneRecord> CurrentReadyAsync(Guid survivorId, int sequence, CancellationToken ct)
    {
        var survivor = await db.Survivors.SingleAsync(x => x.Id == survivorId, ct);
        var scene = await db.Set<GeneratedSceneRecord>().SingleOrDefaultAsync(x => x.SurvivorId == survivorId &&
            x.TimelineId == survivor.TimelineId && x.Sequence == sequence, ct);
        if (scene is null || scene.Status != GeneratedSceneStatus.Ready || scene.CompletedAt is not null)
            throw new InvalidOperationException("That scene is not the current ready scene.");
        return scene;
    }

    private static SceneSpec? Spec(GeneratedSceneRecord scene) =>
        scene.Spec is null ? null : JsonSerializer.Deserialize<SceneSpec>(scene.Spec, Json);

    private static StorySceneView ToView(GeneratedSceneRecord scene, int day) =>
        new(scene.Status, scene.Sequence, day, scene.Status == GeneratedSceneStatus.Failed ? scene.Error : null,
            scene.Status == GeneratedSceneStatus.Ready ? Spec(scene) : null);
}
