using System.Text.Json;
using Fatewake.GameEngine;
using Fatewake.Infrastructure.Persistence;
using Fatewake.Infrastructure.Rag;
using Fatewake.Infrastructure.Work;
using Fatewake.Observability;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fatewake.Infrastructure.Story;

/// <summary>Retrieves canon, authors, validates and persists one scene; the server owns the pacing rule.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Story/SceneWorkStepHandler.md">Documentation</see>.</remarks>
public sealed class SceneWorkStepHandler(FatewakeDbContext db, IRagRetriever retriever, ISceneAuthor author,
    SceneAuthoringOptions options, ILogger<SceneWorkStepHandler> log) : IWorkStepHandler
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    /// <inheritdoc />
    public string StepType => SceneWork.StepType;

    /// <inheritdoc />
    public async Task<string?> ExecuteAsync(WorkStepExecutionContext context, CancellationToken ct)
    {
        using var operation = OperationTelemetry.Start("story.scene.work", log);
        GeneratedSceneRecord scene;
        try
        {
            var request = JsonSerializer.Deserialize<SceneGenerationRequest>(context.Input)
                ?? throw new InvalidDataException("Invalid scene work input.");
            scene = await db.Set<GeneratedSceneRecord>().SingleAsync(x => x.Id == request.SceneId, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            operation.Fail(ex);
            throw;
        }
        if (scene.Status == GeneratedSceneStatus.Ready)
            return Result("ready");
        try
        {
            if (!options.Enabled || string.IsNullOrWhiteSpace(options.ApiKey))
                return await FailAsync(scene, "Scene authoring is disabled; set SceneAuthoring:Enabled and SceneAuthoring:ApiKey.", ct);
            var previous = scene.Sequence > 1
                ? await db.Set<GeneratedSceneRecord>().SingleOrDefaultAsync(x => x.SurvivorId == scene.SurvivorId &&
                    x.TimelineId == scene.TimelineId && x.Sequence == scene.Sequence - 1, ct)
                : null;
            if (scene.Sequence > 1 && previous?.Status != GeneratedSceneStatus.Ready)
                throw new TimeoutException("The previous scene is not ready yet.");
            var streak = previous?.DecisionFreeStreak ?? 0;
            var survivor = await db.Set<SurvivorRecord>().SingleAsync(x => x.Id == scene.SurvivorId, ct);
            var recent = await db.Set<GeneratedSceneRecord>().Where(x => x.SurvivorId == scene.SurvivorId &&
                    x.TimelineId == scene.TimelineId && x.Sequence < scene.Sequence && x.Status == GeneratedSceneStatus.Ready)
                .OrderByDescending(x => x.Sequence).Take(4).Select(x => x.Summary!).ToListAsync(ct);
            recent.Reverse();
            var query = $"{survivor.DisplayName} day {survivor.SurvivorDay} " +
                (recent.Count > 0 ? recent[^1] : "opening scene setting characters world");
            var passages = await retriever.SearchAsync(query, new RagSearchFilter(scene.SurvivorId,
                SceneWork.TimelineKey(scene.TimelineId), 1, survivor.SurvivorDay), options.RetrievalLimit, ct);
            if (passages.Count == 0)
                return await FailAsync(scene, "No approved story canon was found in the RAG corpus; import and approve the world bible first.", ct);

            var spec = await author.AuthorAsync(new SceneAuthoringContext(survivor.DisplayName, survivor.SurvivorDay,
                scene.Sequence, SceneDecisionPolicy.RequiresDecision(streak),
                passages.Select(x => x.Text.Length > 1200 ? x.Text[..1200] : x.Text).ToList(), recent,
                survivor.AccountId, survivor.Id, PlayerChoiceText(previous)), context.JobId, context.Attempt, ct);
            scene.DecisionFreeStreak = SceneSpecValidator.Validate(spec, streak);
            scene.Title = spec.Title.Trim();
            scene.Summary = spec.Summary.Trim();
            scene.Spec = JsonSerializer.Serialize(spec, Json);
            scene.HasDecision = spec.HasDecision;
            scene.SurvivorDay = survivor.SurvivorDay;
            scene.Status = GeneratedSceneStatus.Ready;
            scene.Error = null;
            scene.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            log.LogInformation("Scene {SceneSequence} is ready with decision {HasDecision}", scene.Sequence, scene.HasDecision);
            return Result("ready");
        }
        catch (OperationCanceledException ex) when (ct.IsCancellationRequested)
        {
            operation.Fail(ex);
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutException)
        {
            operation.Fail(ex);
            if (context.Attempt >= 5)
                await FailAsync(scene, "Scene generation retries were exhausted.", ct);
            throw;
        }
        catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException)
        {
            operation.Fail(ex);
            log.LogWarning(ex, "Scene generation produced no usable scene on attempt {WorkAttempt}", context.Attempt);
            if (context.Attempt >= 3)
                return await FailAsync(scene, "The narrator did not produce a valid scene.", ct);
            throw;
        }
    }

    private async Task<string> FailAsync(GeneratedSceneRecord scene, string error, CancellationToken ct)
    {
        scene.Status = GeneratedSceneStatus.Failed;
        scene.Error = error;
        scene.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result("failed");
    }

    private static string Result(string status) => JsonSerializer.Serialize(new { status });

    private static string? PlayerChoiceText(GeneratedSceneRecord? previous)
    {
        if (previous?.ChosenActionType is null)
            return null;
        if (!string.IsNullOrWhiteSpace(previous.ChosenText))
            return previous.ChosenText;
        var spec = previous.Spec is null ? null : JsonSerializer.Deserialize<SceneSpec>(previous.Spec, Json);
        return spec?.Choices.FirstOrDefault(x => x.ActionType == previous.ChosenActionType)?.Label;
    }
}
