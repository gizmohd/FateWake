using Fatewake.Infrastructure.Art;
using Fatewake.Infrastructure.Persistence;
using Fatewake.Observability;
using Fatewake.Story;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Fatewake.Api;

/// <summary>Queues authored scene layers and delivers approved artwork.</summary>
/// <see href="../../../docs/code/src/Fatewake.Api/SceneArtEndpoints.md">SceneArtEndpoints documentation</see>
public static class SceneArtEndpoints
{
    /// <summary>Registers scene resolution and immutable binary delivery routes.</summary>
    public static void MapSceneArt(this WebApplication app)
    {
        var log = app.Logger;
        app.MapPost("/api/art/scene/resolve", async (SceneArtRequest request, SceneArtCatalog catalog,
            IArtAssetStore assets, IArtBinaryStorage storage, IArtWorkJobFactory jobs, FatewakeDbContext db,
            HttpContext context, CancellationToken ct) =>
        {
            using var operation = OperationTelemetry.Start("art.scene.resolve", log);
            try
            {
                if (request.Keys is null || request.Keys.Length is < 1 or > 24 || request.Keys.Any(key => !catalog.Contains(key)))
                    return Results.BadRequest(new { error = "Unknown or invalid scene asset keys." });
                if (request.SelectedActionType is not null &&
                    !DayOneScenes.Opening.Beats.SelectMany(x => x.Interactions)
                        .Any(x => x.ActionType == request.SelectedActionType))
                    return Results.BadRequest(new { error = "Unknown selected scene action." });
                if (context.Request.Headers.ContainsKey("Authorization") && context.User.Identity?.IsAuthenticated != true)
                    return Results.Unauthorized();
                Guid? accountId = Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var owner) ? owner : null;
                if (request.SurvivorId is { } survivor &&
                    !await db.Survivors.AnyAsync(x => x.Id == survivor && x.AccountId == accountId, ct))
                    return Results.NotFound();

                var results = new List<SceneArtResult>();
                foreach (var key in request.Keys.Distinct(StringComparer.Ordinal))
                {
                    var work = (await SceneArtRevision.CreateAsync(catalog, db, key, ct)) with
                    {
                        AccountId = accountId,
                        SurvivorId = request.SurvivorId,
                        Priority = request.SelectedActionType is null ? 0 : 100
                    };
                    var resolution = await assets.FindApprovedByFingerprintAsync(work.VisualFingerprint, ct, work.IsPrivate ? accountId : null);
                    while (resolution is { } asset && !await HasCompleteBinariesAsync(asset, storage, ct))
                    {
                        work = CreateRecoveryRequest(work, asset.Id);
                        resolution = await assets.FindApprovedByFingerprintAsync(work.VisualFingerprint, ct, work.IsPrivate ? accountId : null);
                    }
                    if (resolution is { } readyAsset)
                    {
                        results.Add(new(key, "ready", readyAsset.Version));
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
                        var completed = await assets.FindApprovedByFingerprintAsync(work.VisualFingerprint, ct, work.IsPrivate ? accountId : null);
                        if (completed is { } ready && await HasCompleteBinariesAsync(ready, storage, ct))
                        {
                            results.Add(new(key, "ready", ready.Version));
                            continue;
                        }
                    }

                    results.Add(new(key, status switch
                    {
                        WorkJobStatus.Failed => "failed",
                        WorkJobStatus.Cancelled => "cancelled",
                        WorkJobStatus.Completed => "failed",
                        _ => "pending"
                    }, JobId: job.JobId));
                }

                return Results.Ok(results);
            }
            catch (OperationCanceledException ex) when (ct.IsCancellationRequested)
            {
                operation.Fail(ex);
                throw;
            }
            catch (Exception ex)
            {
                operation.Fail(ex);
                log.LogError(ex, "Scene artwork resolution failed");
                throw;
            }
        });

        app.MapGet("/api/art/{key}/{version:int}/{format}", async (string key, int version, string format,
            FatewakeDbContext db, IArtBinaryStorage storage, HttpContext context, CancellationToken ct) =>
        {
            if (version < 1 || format is not ("png" or "webp")) return Results.NotFound();
            var asset = await db.ArtAssets.AsNoTracking()
                .Where(x => x.Key == key && x.Version == version && x.Status == ArtAssetStatus.Approved)
                .Select(x => new { x.Id, x.OwnerAccountId })
                .FirstOrDefaultAsync(ct);
            if (asset is null) return Results.NotFound();
            if (asset.OwnerAccountId is Guid owner &&
                (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var caller) || caller != owner))
                return Results.NotFound();

            var derivativeKind = format == "webp" ? ArtDerivativeKind.WebP : ArtDerivativeKind.OptimizedPng;
            var derivative = await db.ArtAssetDerivatives.AsNoTracking()
                .Where(x => x.ArtAssetId == asset.Id && x.Kind == derivativeKind)
                .Select(x => new { x.StorageKey, x.ContentType, x.ContentHash, x.ByteSize, x.CreatedAt })
                .FirstOrDefaultAsync(ct);
            if (derivative is null || !await storage.ExistsAsync(derivative.StorageKey, ct)) return Results.NotFound();

            var etag = $"\"{derivative.ContentHash}\"";
            context.Response.ContentLength = derivative.ByteSize;
            context.Response.Headers.ETag = etag;
            context.Response.Headers.CacheControl = asset.OwnerAccountId is null
                ? "public,max-age=31536000,immutable"
                : "private,max-age=3600,immutable";
            if (asset.OwnerAccountId is not null) context.Response.Headers.Vary = "Authorization";
            context.Response.Headers.LastModified = derivative.CreatedAt.ToUniversalTime().ToString("R");
            if (MatchesIfNoneMatch(context.Request.Headers["If-None-Match"], etag))
                return Results.StatusCode(StatusCodes.Status304NotModified);

            return Results.Stream(await storage.OpenReadAsync(derivative.StorageKey, ct), derivative.ContentType);
        });
    }

    private static async Task<bool> HasCompleteBinariesAsync(ArtAssetMatch asset, IArtBinaryStorage storage, CancellationToken ct)
        => asset.WebPStorageKey is { Length: > 0 } webpKey &&
           asset.OptimizedPngStorageKey is { Length: > 0 } pngKey &&
           await storage.ExistsAsync(asset.MasterPngStorageKey, ct) &&
           await storage.ExistsAsync(webpKey, ct) &&
           await storage.ExistsAsync(pngKey, ct);

    private static ArtWorkRequest CreateRecoveryRequest(ArtWorkRequest request, Guid missingAssetId)
    {
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{request.VisualFingerprint}\nmissing-binary-recovery\n{missingAssetId:N}"))).ToLowerInvariant();
        return request with { VisualFingerprint = fingerprint };
    }

    private static bool MatchesIfNoneMatch(IEnumerable<string> values, string etag)
    {
        foreach (var value in values)
            foreach (var candidate in value.Split(','))
            {
                var normalized = candidate.Trim();
                if (normalized.StartsWith("W/", StringComparison.OrdinalIgnoreCase)) normalized = normalized[2..].Trim();
                if (normalized == "*" || string.Equals(normalized, etag, StringComparison.Ordinal)) return true;
            }
        return false;
    }
}
