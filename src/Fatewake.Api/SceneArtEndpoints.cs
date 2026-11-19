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
            SceneArtPromptService prompts,
            IArtAssetStore assets, IArtBinaryStorage storage, IArtWorkJobFactory jobs, FatewakeDbContext db,
            HttpContext context, CancellationToken ct) =>
        {
            using var operation = OperationTelemetry.Start("art.scene.resolve", log);
            try
            {
                if (request.Keys is null || request.Keys.Length is < 1 or > 24 || request.Keys.Any(key => !catalog.Contains(key)))
                    return Results.BadRequest(new
                    {
                        error = "Unknown or invalid scene asset keys."
                    });
                if (request.SelectedActionType is not null && !SceneArtContextPolicy.IsKnownAction(request.SelectedActionType))
                    return Results.BadRequest(new
                    {
                        error = "Unknown selected scene action."
                    });
                if (request.SelectedActionType is not null && request.SurvivorId is null)
                    return Results.BadRequest(new
                    {
                        error = "A reached survivor scene is required to prioritize a selected action."
                    });
                if (context.Request.Headers.ContainsKey("Authorization") && context.User.Identity?.IsAuthenticated != true)
                    return Results.Unauthorized();
                Guid? accountId = Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var owner) ? owner : null;
                var needsReachedState = request.SelectedActionType is not null ||
                    request.Keys.Any(SceneArtContextPolicy.RequiresReachedOutcome);
                string? activeBeat = null;
                string? lastOutcome = null;
                if (request.SurvivorId is { } survivor)
                {
                    if (needsReachedState)
                    {
                        var reached = await ReadReachedStateAsync(db, survivor, accountId, ct);
                        if (!reached.Owned)
                            return Results.NotFound();
                        activeBeat = reached.BeatKey;
                        lastOutcome = reached.LastOutcome;
                        if (request.SelectedActionType is not null &&
                            !SceneArtContextPolicy.IsSelectedActionReached(request.SelectedActionType, activeBeat, lastOutcome))
                            return Results.BadRequest(new
                            {
                                error = "The selected action is not available at the survivor's reached scene."
                            });
                    }
                    else if (!await db.Survivors.AnyAsync(x => x.Id == survivor && x.AccountId == accountId, ct))
                        return Results.NotFound();
                }

                var results = new List<SceneArtResult>();
                foreach (var key in request.Keys.Distinct(StringComparer.Ordinal))
                {
                    string? reachedVisualContext = null;
                    if (SceneArtContextPolicy.RequiresReachedOutcome(key))
                    {
                        if (activeBeat != "choice-wake" || string.IsNullOrWhiteSpace(lastOutcome))
                        {
                            results.Add(new(key, "not-reached"));
                            continue;
                        }
                        if (!SceneArtContextPolicy.TryGetReachedVisualContext(lastOutcome, out var safeContext))
                        {
                            results.Add(new(key, "failed"));
                            continue;
                        }
                        reachedVisualContext = safeContext;
                    }
                    var prompt = await prompts.ResolveAsync(key, accountId, request.SurvivorId,
                        request.SelectedActionType is null ? 0 : 100, ct, reachedVisualContext);
                    if (prompt.Status != "ready" || prompt.WorkRequest is null)
                    {
                        var prior = await FindPriorStyleAsync(db, assets, storage,
                            catalog.Describe(key, reachedVisualContext), ct);
                        results.Add(prior is not null
                            ? new(key, "ready", prior.Version, ReplacementPending: prompt.Status == "pending")
                            : new(key, prompt.Status == "disabled" ? "prompt-disabled" : prompt.Status, JobId: prompt.JobId));
                        continue;
                    }
                    var work = prompt.WorkRequest;
                    var resolution = await assets.FindApprovedByFingerprintAsync(work.VisualFingerprint, ct, work.IsPrivate ? accountId : null);
                    if (resolution is null && work.LegacyVisualFingerprint is { Length: > 0 } legacyFingerprint)
                        resolution = await assets.FindApprovedByFingerprintAsync(legacyFingerprint, ct,
                            work.IsPrivate ? accountId : null);
                    while (resolution is { } asset && !await HasCompleteBinariesAsync(asset, storage, ct))
                    {
                        work = CreateRecoveryRequest(work, asset.Id);
                        resolution = await assets.FindApprovedByFingerprintAsync(work.VisualFingerprint, ct, work.IsPrivate ? accountId : null);
                    }
                    if (resolution is { } readyAsset)
                    {
                        results.Add(new(key, "ready", readyAsset.Version, ReplacementPending: prompt.ReplacementPending));
                        continue;
                    }

                    if (!work.IsPrivate)
                    {
                        var prior = await FindPriorStyleAsync(db, assets, storage,
                            catalog.Describe(key, reachedVisualContext), ct);
                        if (prior is not null)
                        {
                            results.Add(new(key, "ready", prior.Version, ReplacementPending: prompt.ReplacementPending));
                            continue;
                        }
                    }

                    if (prompt.ReplacementPending)
                    {
                        results.Add(new(key, "pending"));
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
                            results.Add(new(key, "ready", ready.Version, ReplacementPending: prompt.ReplacementPending));
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
            var sprite = format == "sprite-png";
            if (version < 1 || format is not ("png" or "webp" or "sprite-png") ||
                (sprite && !key.StartsWith("char-", StringComparison.Ordinal)))
                return Results.NotFound();
            Guid? caller = Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var owner) ? owner : null;
            var derivativeKind = format == "webp" ? ArtDerivativeKind.WebP : ArtDerivativeKind.OptimizedPng;
            var derivative = await (from asset in db.ArtAssets.AsNoTracking()
                                    join file in db.ArtAssetDerivatives.AsNoTracking() on asset.Id equals file.ArtAssetId
                                    where asset.Key == key && asset.Version == version && asset.Status == ArtAssetStatus.Approved &&
                                        (asset.OwnerAccountId == null || asset.OwnerAccountId == caller) && file.Kind == derivativeKind
                                    select new { file.StorageKey, file.ContentType, file.ContentHash, file.ByteSize, file.CreatedAt,
                                        asset.OwnerAccountId }).FirstOrDefaultAsync(ct);
            if (derivative is null)
                return Results.NotFound();

            var etag = sprite ? $"\"{derivative.ContentHash}-sprite-v1\"" : $"\"{derivative.ContentHash}\"";
            var notModified = MatchesIfNoneMatch(context.Request.Headers["If-None-Match"], etag);
            Stream? source = null;
            if (notModified)
            {
                if (!await storage.ExistsAsync(derivative.StorageKey, ct))
                    return Results.NotFound();
            }
            else
            {
                try
                {
                    source = await storage.OpenReadAsync(derivative.StorageKey, ct);
                }
                catch (IOException ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
                {
                    return Results.NotFound();
                }
            }
            context.Response.Headers.ETag = etag;
            context.Response.Headers.CacheControl = derivative.OwnerAccountId is null
                ? "public,max-age=31536000,immutable"
                : "private,max-age=3600,immutable";
            if (derivative.OwnerAccountId is not null)
                context.Response.Headers.Vary = "Authorization";
            context.Response.Headers.LastModified = derivative.CreatedAt.ToUniversalTime().ToString("R");
            if (notModified)
                return Results.StatusCode(StatusCodes.Status304NotModified);

            if (sprite)
            {
                await using var spriteSource = source!;
                using var buffer = new MemoryStream();
                await spriteSource.CopyToAsync(buffer, ct);
                var png = buffer.ToArray();
                return Results.File(CharacterSprite.CropPng(png) ?? png, "image/png");
            }

            context.Response.ContentLength = derivative.ByteSize;
            return Results.Stream(source!, derivative.ContentType);
        });
    }

    private static async Task<(bool Owned, string? BeatKey, string? LastOutcome)> ReadReachedStateAsync(
        FatewakeDbContext db, Guid survivorId, Guid? accountId, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var survivor = await db.Survivors
            .FromSqlInterpolated($"""SELECT * FROM survivor WHERE "Id" = {survivorId} FOR UPDATE""")
            .SingleOrDefaultAsync(ct);
        if (survivor is null || survivor.AccountId != accountId)
            return (false, null, null);
        await db.Entry(survivor).ReloadAsync(ct);
        var episode = await db.EventInstances.AsNoTracking().SingleOrDefaultAsync(row =>
            row.SurvivorId == survivor.Id && row.TimelineId == survivor.TimelineId &&
            row.Status == EventInstanceStatus.Active, ct);
        if (episode is null || episode.SurvivorDay != 1 || episode.EventKey != "day-001-injured-stranger")
        {
            await transaction.CommitAsync(ct);
            return (true, null, null);
        }
        using var state = System.Text.Json.JsonDocument.Parse(episode.State);
        var root = state.RootElement;
        var sceneKey = root.TryGetProperty("sceneKey", out var scene) ? scene.GetString() : null;
        var beatKey = root.TryGetProperty("beatKey", out var beat) ? beat.GetString() : null;
        var outcome = root.TryGetProperty("lastOutcome", out var last) && last.ValueKind == System.Text.Json.JsonValueKind.String
            ? last.GetString() : null;
        await transaction.CommitAsync(ct);
        return sceneKey == DayOneScenes.Opening.Key
            ? (true, beatKey, outcome)
            : (true, null, null);
    }

    private static async Task<ArtAssetMatch?> FindPriorStyleAsync(FatewakeDbContext db, IArtAssetStore assets,
        IArtBinaryStorage storage, SceneArtPromptMetadata metadata, CancellationToken ct)
    {
        var fingerprints = await (
            from asset in db.ArtAssets.AsNoTracking()
            join generation in db.ArtGenerations.AsNoTracking() on asset.Id equals generation.ArtAssetId
            join revision in db.Set<ArtPromptRevisionRecord>().AsNoTracking() on generation.PromptRevisionId equals revision.Id
            join prompt in db.Set<ArtPromptRecord>().AsNoTracking() on revision.ArtPromptId equals prompt.Id
            where asset.Key == metadata.AssetKey && asset.Status == ArtAssetStatus.Approved &&
                asset.OwnerAccountId == null && !prompt.IsPrivate &&
                prompt.ContextFingerprint == metadata.ContextFingerprint && prompt.StyleBibleVersion != metadata.StyleBibleVersion
            orderby asset.Version descending
            select asset.VisualFingerprint).ToArrayAsync(ct);
        foreach (var fingerprint in fingerprints)
        {
            var candidate = await assets.FindApprovedByFingerprintAsync(fingerprint, ct);
            if (candidate is not null && await HasCompleteBinariesAsync(candidate, storage, ct))
                return candidate;
        }
        return null;
    }

    private static async Task<bool> HasCompleteBinariesAsync(ArtAssetMatch asset, IArtBinaryStorage storage, CancellationToken ct)
        => asset.WebPStorageKey is { Length: > 0 } webpKey &&
           asset.OptimizedPngStorageKey is { Length: > 0 } pngKey &&
           await storage.AllExistAsync([asset.MasterPngStorageKey, webpKey, pngKey], ct);

    private static ArtWorkRequest CreateRecoveryRequest(ArtWorkRequest request, Guid missingAssetId)
    {
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{request.VisualFingerprint}\nmissing-binary-recovery\n{missingAssetId:N}"))).ToLowerInvariant();
        return request with
        {
            VisualFingerprint = fingerprint,
            LegacyVisualFingerprint = null
        };
    }

    private static bool MatchesIfNoneMatch(IEnumerable<string> values, string etag)
    {
        foreach (var value in values)
            foreach (var candidate in value.Split(','))
            {
                var normalized = candidate.Trim();
                if (normalized.StartsWith("W/", StringComparison.OrdinalIgnoreCase))
                    normalized = normalized[2..].Trim();
                if (normalized == "*" || string.Equals(normalized, etag, StringComparison.Ordinal))
                    return true;
            }
        return false;
    }
}
