using System.Text.Json;
using Fatewake.GameEngine;
using Fatewake.Infrastructure.Authentication;
using Fatewake.Story;
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
            var level = (await AccountProfileService.GetContentAsync(db, survivor.AccountId, ct)).Level;
            var recent = await db.Set<GeneratedSceneRecord>().Where(x => x.SurvivorId == scene.SurvivorId &&
                    x.TimelineId == scene.TimelineId && x.Sequence < scene.Sequence && x.Status == GeneratedSceneStatus.Ready &&
                    x.ContentLevel != null && x.ContentLevel <= level)
                .OrderByDescending(x => x.Sequence).Take(4).Select(x => x.Summary!).ToListAsync(ct);
            recent = recent.Where(text => StoryContentPolicy.Allows(level, text)).ToList();
            recent.Reverse();
            var dayOne = scene.Sequence == 1
                ? await (from episode in db.EventInstances
                         join attempt in db.ActionAttempts on episode.Id equals attempt.EventInstanceId
                         join result in db.ActionResolutions on attempt.Id equals result.ActionAttemptId
                         where episode.SurvivorId == scene.SurvivorId && episode.TimelineId == scene.TimelineId &&
                             episode.EventKey == "day-001-injured-stranger"
                         orderby attempt.SubmittedAt descending
                         select new { result.OutcomeType, attempt.RawInput }).FirstOrDefaultAsync(ct)
                : null;
            if (dayOne is not null)
                recent.Add(DayOneOutcomes.Text(dayOne.OutcomeType));

            var playerChoice = dayOne?.RawInput ?? (previous?.ContentLevel is { } previousLevel && previousLevel <= level
                ? PlayerChoiceText(previous) : null);
            if (!StoryContentPolicy.Allows(level, playerChoice))
                playerChoice = null;
            var intent = StoryBranch.DayOneIntent(dayOne?.OutcomeType) ?? StoryBranch.Intent(playerChoice);
            if (intent is null && previous is { HasDecision: false, ContentLevel: not null } && previous.ContentLevel <= level)
                intent = "continue_without_decision";
            var arrival = SessionStore.Arrival(survivor) ?? DayOneArrival.Original;
            if (intent is not null && scene.Sequence == 1)
                intent = $"{arrival.SiteKey}:{arrival.Minute}:{intent}";
            var branchKey = StoryBranch.Key(survivor.SurvivorDay, scene.Sequence, intent, previous?.BranchKey);
            var focus = branchKey is null ? null : await (
                from candidate in db.Set<GeneratedSceneRecord>()
                join owner in db.Survivors on candidate.SurvivorId equals owner.Id
                where candidate.BranchKey == branchKey && candidate.ContentLevel == level && candidate.BranchFocus != null &&
                    candidate.SurvivorId != survivor.Id && (survivor.AccountId == null || owner.AccountId != survivor.AccountId) &&
                    candidate.Status == GeneratedSceneStatus.Ready
                group candidate by candidate.BranchFocus into matches
                orderby matches.Count() descending, matches.Key
                select matches.Key).FirstOrDefaultAsync(ct);
            if (!StoryBranch.Focuses.Contains(focus))
                focus = null;
            var query = $"{arrival.SiteName} day {survivor.SurvivorDay} " +
                (recent.Count > 0 ? recent[^1] : "opening scene setting characters world");
            var filter = new RagSearchFilter(scene.SurvivorId, SceneWork.TimelineKey(scene.TimelineId),
                1, survivor.SurvivorDay);
            var passages = await retriever.SearchAsync(query, filter, options.RetrievalLimit, ct);
            var claims = await new RagClaimService(db).SearchApprovedAsync(query, filter, options.RetrievalLimit, ct);
            var approvedCanon = claims.Concat(passages.Select(x => x.Text))
                .Where(text => !string.IsNullOrWhiteSpace(text) && StoryContentPolicy.Allows(level, text))
                .Select(text => text.Length > 1200 ? text[..1200] : text).ToArray();
            var threadCanon = await ThreadCanon.ReadAsync(db, scene, survivor.SurvivorDay, level, 8, ct);
            var canon = approvedCanon.Concat(threadCanon.Where(text => StoryContentPolicy.Allows(level, text)))
                .Distinct(StringComparer.Ordinal).ToArray();

            var spec = await author.AuthorAsync(new SceneAuthoringContext(survivor.DisplayName, survivor.SurvivorDay,
                scene.Sequence, SceneDecisionPolicy.RequiresDecision(streak),
                canon, recent,
                survivor.AccountId, survivor.Id, playerChoice, level, focus, arrival), context.JobId, context.Attempt, ct);
            if (!StoryContentPolicy.AllowsScene(level, spec) ||
                spec.Choices.Count > 2 || spec.Choices.Any(x => x.AllowFreeform || x.ActionType == SceneChoiceResolver.Freeform))
                throw new InvalidDataException("The scene did not pass content or choice checks.");

            if (spec.Choices.Count > 0)
                spec = spec with { Choices = [..spec.Choices, new(SceneChoiceResolver.Freeform, "Do something else…", true)] };
            scene.DecisionFreeStreak = SceneSpecValidator.Validate(spec, streak);
            scene.ContentLevel = level;
            scene.BranchKey = branchKey;
            scene.BranchFocus = StoryBranch.Focuses.Contains(spec.BranchFocus) ? spec.BranchFocus : null;
            spec = spec with { BranchFocus = null };
            scene.Title = spec.Title.Trim();
            scene.Summary = spec.Summary.Trim();
            scene.Spec = JsonSerializer.Serialize(spec, Json);
            scene.HasDecision = spec.HasDecision;
            scene.SurvivorDay = survivor.SurvivorDay;
            scene.Status = GeneratedSceneStatus.Ready;
            scene.Error = null;
            scene.UpdatedAt = DateTimeOffset.UtcNow;
            if (approvedCanon.Length == 0)
                ThreadCanon.Add(db, scene, scene.Summary);
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
