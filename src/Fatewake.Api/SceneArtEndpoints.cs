using Fatewake.Infrastructure.Art;
using Fatewake.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Fatewake.Api;

/// <summary>Queues authored scene layers and delivers approved artwork.</summary>
public static class SceneArtEndpoints
{
    /// <summary>Registers scene resolution and immutable binary delivery routes.</summary>
    public static void MapSceneArt(this WebApplication app)
    {
        app.MapPost("/api/art/scene/resolve", async (SceneArtRequest request, SceneArtCatalog catalog,
            IArtAssetStore assets, IArtWorkJobFactory jobs, FatewakeDbContext db, HttpContext context, CancellationToken ct) =>
        {
            if (request.Keys is null || request.Keys.Length is < 1 or > 24 || request.Keys.Any(key => !catalog.Contains(key)))
                return Results.BadRequest(new { error = "Unknown or invalid scene asset keys." });
            if(context.Request.Headers.ContainsKey("Authorization")&&context.User.Identity?.IsAuthenticated!=true) return Results.Unauthorized();
            Guid? accountId=Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier),out var owner)?owner:null;
            if(request.SurvivorId is {} survivor && !await db.Survivors.AnyAsync(x=>x.Id==survivor&&x.AccountId==accountId,ct)) return Results.NotFound();
            var results = new List<SceneArtResult>();
            foreach (var key in request.Keys.Distinct(StringComparer.Ordinal))
            {
                var work = (await SceneArtRevision.CreateAsync(catalog, db, key, ct)) with { AccountId=accountId, SurvivorId=request.SurvivorId };
                var resolution = await assets.FindApprovedByFingerprintAsync(work.VisualFingerprint, ct);
                if (resolution is {} asset)
                {
                    results.Add(new(key, "ready", asset.Version));
                    continue;
                }
                if (!catalog.GenerationEnabled)
                {
                    results.Add(new(key, "disabled"));
                    continue;
                }
                var job = await jobs.CreateAsync(work, ct);
                var status = await db.WorkJobs.AsNoTracking().Where(x => x.Id == job.JobId).Select(x => x.Status).SingleAsync(ct);
                // Finalization can commit between the initial lookup and the status query.
                if (status == WorkJobStatus.Completed)
                {
                    var completed = await assets.FindApprovedByFingerprintAsync(work.VisualFingerprint, ct);
                    if (completed is {} ready)
                    {
                        results.Add(new(key, "ready", ready.Version));
                        continue;
                    }
                }
                results.Add(new(key, status switch
                {
                    WorkJobStatus.Failed => "failed", WorkJobStatus.Cancelled => "cancelled",
                    WorkJobStatus.Completed => "failed", _ => "pending"
                }, JobId: job.JobId));
            }
            return Results.Ok(results);
        });

        app.MapGet("/api/art/{key}/{version:int}/{format}", async (string key, int version, string format,
            SceneArtCatalog catalog, IArtAssetStore assets, IArtBinaryStorage storage, CancellationToken ct) =>
        {
            if (!catalog.Contains(key) || version < 1 || format is not ("png" or "webp")) return Results.NotFound();
            var asset = await assets.FindApprovedAsync(key, version, ct);
            var storageKey = format == "webp" ? asset?.WebPStorageKey : asset?.OptimizedPngStorageKey ?? asset?.MasterPngStorageKey;
            if (storageKey is null || !await storage.ExistsAsync(storageKey, ct)) return Results.NotFound();
            return Results.Stream(await storage.OpenReadAsync(storageKey, ct), "image/" + format);
        });
    }
}

/// <summary>A bounded set of authored layer keys.</summary>
public sealed record SceneArtRequest(string[]? Keys,Guid? SurvivorId=null);
/// <summary>Reports asset availability or durable generation state.</summary>
public sealed record SceneArtResult(string Key, string Status, int? Version = null, Guid? JobId = null);
