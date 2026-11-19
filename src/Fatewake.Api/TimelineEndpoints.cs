using System.Security.Claims;
using System.Text.Json;
using Fatewake.Story;
using Fatewake.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fatewake.Api;

/// <summary>Provides owner-only checkpoint discovery and explicit non-destructive branch mutations.</summary>
/// <see href="../../docs/code/src/Fatewake.Api/TimelineEndpoints.md">Documentation</see>
public static class TimelineEndpoints
{
    /// <summary>Maps owner-only checkpoint listing and branch mutation routes.</summary>
    public static void MapTimelines(this WebApplication app)
    {
        var group = app.MapGroup("/api/timelines/{survivorId:guid}").RequireAuthorization();
        group.MapGet("", async (Guid survivorId, int? page, HttpContext context, FatewakeDbContext db, CancellationToken ct) =>
        {
            if (!await OwnsAsync(db, survivorId, context.User, ct))
                return Results.NotFound();
            var survivor = await db.Survivors.AsNoTracking().SingleAsync(row => row.Id == survivorId, ct);
            var current = await db.EventInstances.AsNoTracking().SingleAsync(row => row.SurvivorId == survivorId &&
                row.TimelineId == survivor.TimelineId && row.Status == EventInstanceStatus.Active, ct);
            var opening = SessionStore.Arrival(survivor)?.Opening() ?? DayOneScenes.Opening;
            var index = Math.Clamp(page ?? 0, 0, 100000);
            var branches = await db.Set<TimelineBranchRecord>().AsNoTracking()
                .Where(row => row.SurvivorId == survivorId).OrderBy(row => row.CreatedAt).ThenBy(row => row.TimelineId)
                .Skip(index * 20).Take(21)
                .Select(row => new
                {
                    row.TimelineId,
                    row.ParentTimelineId,
                    row.CheckpointId,
                    row.Origin,
                    row.CreatedAt,
                    CheckpointLabel = db.Set<TimelineCheckpointRecord>().Where(checkpoint => checkpoint.Id == row.CheckpointId)
                        .Select(checkpoint => checkpoint.Label).FirstOrDefault(),
                    SurvivorDay = db.Set<SurvivorTimelineStateRecord>().Where(state => state.SurvivorId == survivorId && state.TimelineId == row.TimelineId)
                        .Select(state => (int?)state.SurvivorDay).FirstOrDefault()
                }).ToArrayAsync(ct);
            var ids = branches.Take(20).Select(row => row.TimelineId).ToArray();
            var episodes = await db.EventInstances.AsNoTracking().Where(row => row.SurvivorId == survivorId &&
                ids.Contains(row.TimelineId) && row.Status == EventInstanceStatus.Active)
                .Select(row => new { row.TimelineId, row.State }).ToDictionaryAsync(row => row.TimelineId, row => row.State, ct);
            return Results.Ok(new
            {
                survivorId,
                currentTimelineId = survivor.TimelineId,
                eligible = await new TimelineStateStore(db).EligibleAsync(survivor.TimelineId, ct),
                version = current.Version,
                currentSurvivorDay = current.SurvivorDay,
                currentTimeLabel = ReachedTime(current.State, opening),
                page = index,
                hasMore = branches.Length > 20,
                branches = branches.Take(20).Select(row => new
                {
                    row.TimelineId,
                    row.ParentTimelineId,
                    row.CheckpointId,
                    row.Origin,
                    row.CreatedAt,
                    row.CheckpointLabel,
                    row.SurvivorDay,
                    timeLabel = episodes.TryGetValue(row.TimelineId, out var state) ? ReachedTime(state, opening) : null
                })
            });
        });
        group.MapGet("/checkpoints", async (Guid survivorId, int? page, HttpContext context, FatewakeDbContext db, CancellationToken ct) =>
        {
            if (!await OwnsAsync(db, survivorId, context.User, ct))
                return Results.NotFound();
            var timeline = await db.Survivors.Where(row => row.Id == survivorId).Select(row => row.TimelineId).SingleAsync(ct);
            var index = Math.Clamp(page ?? 0, 0, 100000);
            var checkpoints = await db.Set<TimelineCheckpointRecord>().AsNoTracking()
                .Where(row => row.SurvivorId == survivorId && row.TimelineId == timeline)
                .OrderBy(row => row.CreatedAt).ThenBy(row => row.Id).Skip(index * 20).Take(21)
                .Select(row => new { row.Id, row.Label, row.TimeLabel, row.CreatedAt }).ToArrayAsync(ct);
            return Results.Ok(new
            {
                page = index,
                hasMore = checkpoints.Length > 20,
                checkpoints = checkpoints.Take(20)
            });
        });
        group.MapPost("/fork", async (Guid survivorId, TimelineMutationRequest request, HttpContext context,
            FatewakeDbContext db, TimelineBranchService branches, CancellationToken ct) =>
        {
            if (!await OwnsAsync(db, survivorId, context.User, ct))
                return Results.NotFound();
            if (request.ExpectedVersion is null or < 0)
                return Results.BadRequest(new
                {
                    error = "Refresh your timeline before creating a branch."
                });
            return Results.Ok(await branches.ForkAsync(Account(context.User), survivorId, request.ExpectedTimelineId,
                request.CheckpointId, request.RequestId, ct, request.ExpectedVersion));
        }).RequireRateLimiting("timeline-mutation");
        group.MapPost("/select", async (Guid survivorId, TimelineMutationRequest request, HttpContext context,
            FatewakeDbContext db, TimelineBranchService branches, CancellationToken ct) =>
        {
            if (!await OwnsAsync(db, survivorId, context.User, ct))
                return Results.NotFound();
            if (request.TargetTimelineId is not { } target)
                return Results.BadRequest(new
                {
                    error = "A target branch is required."
                });
            if (request.ExpectedVersion is null or < 0)
                return Results.BadRequest(new
                {
                    error = "Refresh your timeline before switching branches."
                });
            return Results.Ok(await branches.SelectAsync(Account(context.User), survivorId, request.ExpectedTimelineId, target, ct, request.ExpectedVersion));
        }).RequireRateLimiting("timeline-mutation");
    }

    private static Guid Account(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private static string? ReachedTime(string state, SceneDefinition opening)
    {
        using var document = JsonDocument.Parse(state);
        if (!document.RootElement.TryGetProperty("sceneKey", out var scene) || scene.ValueKind != JsonValueKind.String ||
            scene.GetString() != opening.Key ||
            !document.RootElement.TryGetProperty("beatKey", out var beat) || beat.ValueKind != JsonValueKind.String)
            return null;
        return opening.Beats.SingleOrDefault(row => row.Key == beat.GetString())?.Time;
    }

    private static Task<bool> OwnsAsync(FatewakeDbContext db, Guid survivorId, ClaimsPrincipal user, CancellationToken ct) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var accountId)
            ? db.Survivors.AnyAsync(row => row.Id == survivorId && row.AccountId == accountId, ct)
            : Task.FromResult(false);
}
