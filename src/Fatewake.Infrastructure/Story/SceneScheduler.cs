using System.Text.Json;
using Fatewake.Infrastructure.Persistence;
using Fatewake.Infrastructure.Work;
using Fatewake.Observability;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fatewake.Infrastructure.Story;

/// <summary>Creates a Pending scene row and its durable job in one transaction.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Story/SceneScheduler.md">Documentation</see>.</remarks>
public sealed class SceneScheduler(FatewakeDbContext db, IWorkJobBuilder jobs, ILogger<SceneScheduler> log) : ISceneScheduler
{
    /// <inheritdoc />
    public async Task<GeneratedSceneRecord> EnsureAsync(Guid survivorId, Guid timelineId, int sequence, bool requeueFailed, CancellationToken ct)
    {
        if (sequence < 1)
            throw new ArgumentOutOfRangeException(nameof(sequence));
        using var operation = OperationTelemetry.Start("story.scene.schedule", log);
        try
        {
            var scene = await db.Set<GeneratedSceneRecord>().AsNoTracking().SingleOrDefaultAsync(
                row => row.SurvivorId == survivorId && row.TimelineId == timelineId && row.Sequence == sequence, ct);
            if (scene is not null && (scene.Status != GeneratedSceneStatus.Failed || !requeueFailed))
                return scene;
            var generation = scene is null ? 0 : scene.UpdatedAt.UtcTicks;
            var id = scene?.Id ?? Guid.NewGuid();
            var input = JsonSerializer.Serialize(new SceneGenerationRequest(id));
            var step = new WorkStepDefinition("generate", SceneWork.StepType, SceneWork.Queue, input, 0, 5);
            await jobs.CreateAsync(new WorkJobDefinition(SceneWork.JobType,
                $"{timelineId:N}:{sequence}:{generation}", input, [step]), async (_, token) =>
                {
                    // The job lock serializes repeated requests for the same scene and retry.
                    scene = await Find(survivorId, timelineId, sequence, token);
                    var now = DateTimeOffset.UtcNow;
                    if (scene is null)
                    {
                        db.Add(scene = new GeneratedSceneRecord
                        {
                            Id = id,
                            SurvivorId = survivorId,
                            TimelineId = timelineId,
                            Sequence = sequence,
                            Status = GeneratedSceneStatus.Pending,
                            CreatedAt = now,
                            UpdatedAt = now
                        });
                    }
                    else if (requeueFailed && scene.Status == GeneratedSceneStatus.Failed &&
                        scene.UpdatedAt.UtcTicks == generation)
                    {
                        scene.Status = GeneratedSceneStatus.Pending;
                        scene.Error = null;
                        scene.UpdatedAt = now;
                    }
                }, ct);
            log.LogInformation("Queued scene generation for sequence {SceneSequence}", sequence);
            return scene!;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            operation.Fail(ex);
            throw;
        }
    }

    private Task<GeneratedSceneRecord?> Find(Guid survivorId, Guid timelineId, int sequence, CancellationToken ct) =>
        db.Set<GeneratedSceneRecord>().SingleOrDefaultAsync(
            x => x.SurvivorId == survivorId && x.TimelineId == timelineId && x.Sequence == sequence, ct);
}
