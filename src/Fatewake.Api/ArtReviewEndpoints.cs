using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Fatewake.Infrastructure.Art;
using Fatewake.Infrastructure.Authentication;
using Fatewake.Infrastructure.Persistence;
using Fatewake.Story;
using Microsoft.EntityFrameworkCore;

namespace Fatewake.Api;

/// <summary>Authorizes administrator critique history and version-pinned SQL prompt revisions.</summary>
public static class ArtReviewEndpoints
{
    /// <summary>Registers the Admin permission policy without assigning roles to ordinary users.</summary>
    public static void AddArtReviewAuthorization(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddTransient<Microsoft.AspNetCore.Authentication.IClaimsTransformation, AdminRoleClaimsTransformation>();
        services.AddScoped<AccountRoleService>();
        services.AddAuthorization(options =>
        {
            options.AddPolicy("ArtAdministrator", policy => policy.RequireAuthenticatedUser().RequireRole("Admin"));
            options.AddPolicy("ArtworkReviewer", policy => policy.RequireAuthenticatedUser().RequireRole("Admin")
                .RequireAssertion(context => Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out _)));
        });
    }

    /// <summary>Maps authenticated review, exact-provenance, preview, revision-save, and replacement routes.</summary>
    public static void MapArtReviews(this WebApplication app)
    {
        app.MapGet("/api/art/reviews", async (string sceneKey, FatewakeDbContext db, CancellationToken ct) =>
            Results.Ok(await db.Set<ArtReviewRecord>().AsNoTracking().Where(x => x.SceneKey == sceneKey)
                .OrderByDescending(x => x.CreatedAt).Take(50).ToArrayAsync(ct)))
            .RequireAuthorization("ArtworkReviewer");

        app.MapPost("/api/art/reviews", SaveReviewAsync).RequireAuthorization("ArtworkReviewer");

        app.MapGet("/api/art/reviews/prompt", async (string assetKey, int version, ClaimsPrincipal user,
            SceneArtCatalog catalog, FatewakeDbContext db, CancellationToken ct) =>
        {
            if (!Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out _))
                return Results.Forbid();
            if (!catalog.Contains(assetKey) || version < 1)
                return Results.BadRequest();
            var asset = await db.ArtAssets.AsNoTracking().SingleOrDefaultAsync(row =>
                row.Key == assetKey && row.Version == version && row.Status == ArtAssetStatus.Approved, ct);
            if (asset is null)
                return Results.NotFound();
            var generation = await db.ArtGenerations.AsNoTracking().Where(row => row.ArtAssetId == asset.Id)
                .OrderByDescending(row => row.CreatedAt).FirstOrDefaultAsync(ct);
            var sourceRevision = generation?.PromptRevisionId is { } sourceRevisionId
                ? await db.Set<ArtPromptRevisionRecord>().AsNoTracking()
                    .SingleOrDefaultAsync(row => row.Id == sourceRevisionId, ct)
                : null;
            var prompt = await FindPromptForAssetAsync(db, catalog, assetKey, asset.Id, ct);
            var metadata = catalog.Describe(assetKey, prompt?.ReachedVisualContext);
            var latestRevision = prompt?.LatestRevisionId is { } latestRevisionId
                ? await db.Set<ArtPromptRevisionRecord>().AsNoTracking()
                    .SingleOrDefaultAsync(row => row.Id == latestRevisionId, ct)
                : null;
            var replacementReview = await db.Set<ArtReviewRecord>().AsNoTracking()
                .Where(row => row.SourceArtAssetId == asset.Id && row.RegenerationWorkJobId != null)
                .OrderByDescending(row => row.CreatedAt).FirstOrDefaultAsync(ct);
            var replacementJobId = replacementReview?.RegenerationWorkJobId;
            var replacementStatus = replacementJobId is { } workId
                ? await ReadReplacementStatusAsync(db, workId, ct)
                : null;
            var replacementVersion = replacementReview?.PromptRevisionId is { } replacementRevisionId
                ? await db.ArtGenerations.AsNoTracking().Where(row => row.PromptRevisionId == replacementRevisionId &&
                        row.ArtAssetId != null)
                    .Join(db.ArtAssets.AsNoTracking(), generationRow => generationRow.ArtAssetId,
                        assetRow => assetRow.Id, (_, assetRow) => (int?)assetRow.Version)
                    .OrderByDescending(value => value).FirstOrDefaultAsync(ct)
                : null;
            var currentWork = latestRevision is null ? null :
                SceneArtRevision.Create(catalog, assetKey, latestRevision, asset.OwnerAccountId,
                    reachedVisualContext: prompt?.ReachedVisualContext);
            return Results.Ok(new ArtPromptReviewResponse(assetKey, version, generation is not null,
                generation?.ResolvedPrompt, generation?.NegativePrompt, generation?.Provider, generation?.Model,
                generation?.ProviderJobId, generation?.PromptRevisionId, sourceRevision?.Source,
                generation?.CreatedAt, prompt?.CurrentRevisionId, prompt?.LatestRevisionId,
                prompt?.LatestRevisionNumber, latestRevision?.PositivePrompt, latestRevision?.NegativePrompt,
                currentWork is null ? string.Empty : catalog.RuntimeStyle,
                currentWork is null ? string.Empty : catalog.RuntimeExclusions,
                currentWork is null ? string.Empty : catalog.Describe(assetKey).Framing,
                replacementStatus, replacementJobId, replacementVersion));
        }).RequireAuthorization("ArtworkReviewer");

        app.MapPost("/api/art/reviews/prompt/preview", (ArtPromptReviewRequest request,
            SceneArtCatalog catalog, FatewakeDbContext db, CancellationToken ct) =>
            PreviewAsync(request, catalog, db, ct)).RequireAuthorization("ArtworkReviewer");

        app.MapPost("/api/art/reviews/prompt", async (ArtPromptReviewRequest request, ClaimsPrincipal user,
            SceneArtCatalog catalog, FatewakeDbContext db, IArtWorkJobFactory jobs, CancellationToken ct) =>
        {
            if (!Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var account))
                return Results.Forbid();
            var validation = Validate(request, catalog);
            if (validation is not null)
                return Results.BadRequest(new
                {
                    error = validation
                });
            if (request.Regenerate && !catalog.GenerationEnabled)
                return Results.Conflict(new
                {
                    error = "Artwork image generation is not configured."
                });

            var asset = await db.ArtAssets.AsNoTracking().SingleOrDefaultAsync(row =>
                row.Key == request.AssetKey && row.Version == request.Version && row.Status == ArtAssetStatus.Approved, ct);
            if (asset is null)
                return Results.NotFound();
            var prompt = await FindPromptForAssetAsync(db, catalog, request.AssetKey, asset.Id, ct);
            if (prompt?.LatestRevisionId is null)
                return Results.Conflict(new
                {
                    error = "A ready SQL prompt revision is required."
                });
            if (prompt.Status != "ready")
                return Results.Conflict(new
                {
                    error = "A ready SQL prompt revision is required."
                });
            var metadata = catalog.Describe(request.AssetKey, prompt.ReachedVisualContext);
            if (prompt.LatestRevisionId != request.ExpectedRevisionId)
                return Results.Conflict(new
                {
                    error = "The prompt changed. Reload the selected layer before saving."
                });

            var revision = new ArtPromptRevisionRecord
            {
                Id = Guid.NewGuid(),
                ArtPromptId = prompt.Id,
                RevisionNumber = (prompt.LatestRevisionNumber ?? 0) + 1,
                ParentRevisionId = prompt.LatestRevisionId,
                PositivePrompt = request.PositivePrompt.Trim(),
                NegativePrompt = string.IsNullOrWhiteSpace(request.NegativePrompt) ? null : request.NegativePrompt.Trim(),
                Source = "admin",
                CreatorAccountId = account,
                RequestContextFingerprint = metadata.ContextFingerprint,
                CreatedAt = DateTimeOffset.UtcNow
            };
            var work = SceneArtRevision.Create(catalog, request.AssetKey, revision,
                asset.OwnerAccountId, priority: 100, reachedVisualContext: prompt.ReachedVisualContext) with
            {
                ForceProviderRegeneration = true,
                IsPrivate = asset.OwnerAccountId is not null
            };
            try
            {
                if (!request.Regenerate)
                {
                    await using var transaction = await db.Database.BeginTransactionAsync(ct);
                    await LockPromptAsync(db, prompt.Id, ct);
                    var current = await db.Set<ArtPromptRecord>().SingleAsync(row => row.Id == prompt.Id, ct);
                    if (current.LatestRevisionId != request.ExpectedRevisionId)
                        return Results.Conflict(new
                        {
                            error = "The prompt changed. Reload the selected layer before saving."
                        });
                    db.Add(revision);
                    current.LatestRevisionId = revision.Id;
                    current.LatestRevisionNumber = revision.RevisionNumber;
                    current.UpdatedAt = revision.CreatedAt;
                    db.Add(CreateReview(account, request, asset.Id, revision.Id, null));
                    await db.SaveChangesAsync(ct);
                    await transaction.CommitAsync(ct);
                    return Results.Ok(new ArtPromptReviewSaveResult(revision.Id, revision.RevisionNumber,
                        work.ResolvedPrompt, work.NegativeInstructions ?? string.Empty, true, false, null));
                }

                var created = await jobs.CreateAsync(work, async (jobId, token) =>
                {
                    await LockPromptAsync(db, prompt.Id, token);
                    var current = await db.Set<ArtPromptRecord>().SingleAsync(row => row.Id == prompt.Id, token);
                    if (current.LatestRevisionId != request.ExpectedRevisionId)
                        throw new PromptRevisionConflictException();
                    db.Add(revision);
                    current.LatestRevisionId = revision.Id;
                    current.LatestRevisionNumber = revision.RevisionNumber;
                    BindReplacement(db, current, revision, asset.Id, work, jobId, "admin-prompt-revision");
                    db.Add(CreateReview(account, request, asset.Id, revision.Id, jobId));
                }, ct);
                return Results.Ok(new ArtPromptReviewSaveResult(revision.Id, revision.RevisionNumber,
                    work.ResolvedPrompt, work.NegativeInstructions ?? string.Empty, true, true, created.JobId));
            }
            catch (PromptRevisionConflictException)
            {
                return Results.Conflict(new
                {
                    error = "The prompt changed. Reload the selected layer before saving."
                });
            }
        }).RequireAuthorization("ArtworkReviewer");
    }

    private static async Task<IResult> SaveReviewAsync(ArtReviewRequest request, ClaimsPrincipal user,
        SceneArtCatalog catalog, FatewakeDbContext db, IArtWorkJobFactory jobs, CancellationToken ct)
    {
        var account = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var versions = request.Versions;
        if (string.IsNullOrWhiteSpace(request.SceneKey) || request.SceneKey.Length > 100 ||
            versions is null || versions.Count is < 1 or > 24 ||
            versions.Any(layer => !catalog.Contains(layer.Key) || layer.Value is <= 0) ||
            request.Critique?.Length > 4000 || request.Correction?.Length > 4000 ||
            (!request.Regenerate && string.IsNullOrWhiteSpace(request.Critique)) ||
            (request.AssetKey is { } selectedKey && (request.Version is null || versions.GetValueOrDefault(selectedKey) != request.Version)))
            return Results.BadRequest(new { error = "Select a rendered layer or composition and provide a critique to save." });

        var rendered = new List<ArtAssetRecord>();
        foreach (var (key, version) in versions)
        {
            if (version is null)
                continue;
            var asset = await db.ArtAssets.AsNoTracking().SingleOrDefaultAsync(row => row.Key == key &&
                row.Version == version && row.Status == ArtAssetStatus.Approved &&
                (row.OwnerAccountId == null || row.OwnerAccountId == account), ct);
            if (asset is null)
                return Results.BadRequest(new { error = "A reviewed version is unavailable. Reload the scene and try again." });
            rendered.Add(asset);
        }
        if (rendered.Count == 0)
            return Results.BadRequest(new { error = "Wait for the scene artwork before reviewing it." });
        var critique = request.Critique?.Trim();
        var visualNote = ArtReviewPreferences.VisualNote(request.Correction ?? critique);
        var review = new ArtReviewRecord
        {
            Id = Guid.NewGuid(),
            AccountId = account,
            SceneKey = request.SceneKey,
            AssetKey = request.AssetKey,
            Version = request.Version,
            CompositionVersions = JsonSerializer.Serialize(versions),
            Critique = string.IsNullOrWhiteSpace(critique) ? "Artwork replacement requested." : critique,
            Correction = request.Correction?.Trim(),
            UseForFutureArtwork = request.UseForFutureArtwork && visualNote is not null &&
                rendered.All(asset => asset.OwnerAccountId is null),
            Regenerate = request.Regenerate,
            CreatedAt = DateTimeOffset.UtcNow
        };
        if (!request.Regenerate)
        {
            db.Add(review);
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { review.Id, queued = false, review.UseForFutureArtwork });
        }
        if (!catalog.GenerationEnabled)
            return Results.Conflict(new { error = "Artwork image generation is not configured." });
        var sceneLayers = DayOneArt.For(request.SceneKey).Select(layer => layer.AssetKey).ToHashSet(StringComparer.Ordinal);
        if (rendered.Any(asset => !sceneLayers.Contains(asset.Key)))
            return Results.BadRequest(new { error = "The selected versions no longer match this scene. Reload it and try again." });

        var targets = new List<(ArtAssetRecord Asset, Guid PromptId)>();
        foreach (var asset in rendered.Where(asset => request.AssetKey is null || asset.Key == request.AssetKey))
        {
            var prompt = await FindPromptForAssetAsync(db, catalog, asset.Key, asset.Id, ct);
            if (prompt is null)
                return Results.Conflict(new { error = $"No editable prompt is available for {asset.Key}." });
            targets.Add((asset, prompt.Id));
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.Add(review);
        var jobIds = new List<Guid>();
        foreach (var (asset, promptId) in targets.OrderBy(target => target.PromptId))
        {
            await LockPromptAsync(db, promptId, ct);
            var prompt = await db.Set<ArtPromptRecord>().SingleAsync(row => row.Id == promptId, ct);
            if (prompt.Status != "ready" || prompt.CurrentRevisionId is not { } currentRevisionId)
                return Results.Conflict(new { error = $"A ready prompt is required for {asset.Key}." });
            if (prompt.RequestedWorkJobId is { } activeJob && await db.WorkJobs.AnyAsync(job => job.Id == activeJob &&
                (job.Status == WorkJobStatus.Pending || job.Status == WorkJobStatus.Running), ct))
                return Results.Conflict(new { error = $"A replacement is already in progress for {asset.Key}." });

            var current = await db.Set<ArtPromptRevisionRecord>().AsNoTracking().SingleAsync(row => row.Id == currentRevisionId, ct);
            var guidance = await ArtReviewPreferences.LoadAsync(db, asset.Key, ct);
            var notes = guidance.Prepend(visualNote).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).Take(4);
            var noteText = string.Join(" ", notes);
            var subject = current.PositivePrompt;
            if (noteText.Length > 0)
            {
                const string label = " Visual preferences, where compatible with the layer role and canon: ";
                var limit = Math.Min(subject.Length, 8000 - label.Length - noteText.Length);
                if (limit > 0 && char.IsHighSurrogate(subject[limit - 1]))
                    limit--;
                subject = subject[..limit].TrimEnd() + label + noteText;
            }

            var revision = new ArtPromptRevisionRecord
            {
                Id = Guid.NewGuid(),
                ArtPromptId = prompt.Id,
                RevisionNumber = (prompt.LatestRevisionNumber ?? 0) + 1,
                ParentRevisionId = prompt.LatestRevisionId,
                PositivePrompt = subject,
                NegativePrompt = current.NegativePrompt,
                Source = "admin",
                CreatorAccountId = account,
                RequestContextFingerprint = current.RequestContextFingerprint,
                CreatedAt = review.CreatedAt
            };
            var work = SceneArtRevision.Create(catalog, asset.Key, revision, asset.OwnerAccountId, priority: 100,
                reachedVisualContext: prompt.ReachedVisualContext) with
            {
                ForceProviderRegeneration = true,
                IsPrivate = asset.OwnerAccountId is not null
            };
            db.Add(revision);
            prompt.LatestRevisionId = revision.Id;
            prompt.LatestRevisionNumber = revision.RevisionNumber;
            var created = await jobs.CreateAsync(work, (jobId, _) =>
            {
                BindReplacement(db, prompt, revision, asset.Id, work, jobId, "admin-scene-regeneration");
                if (request.AssetKey is not null)
                {
                    review.SourceArtAssetId = asset.Id;
                    review.PromptRevisionId = revision.Id;
                    review.RegenerationWorkJobId = jobId;
                }
                return Task.CompletedTask;
            }, ct);
            jobIds.Add(created.JobId);
        }
        await transaction.CommitAsync(ct);
        return Results.Ok(new { review.Id, queued = true, jobIds, review.UseForFutureArtwork });
    }

    private static void BindReplacement(FatewakeDbContext db, ArtPromptRecord prompt, ArtPromptRevisionRecord revision,
        Guid sourceAssetId, ArtWorkRequest work, Guid jobId, string reason)
    {
        prompt.RequestedRevisionId = revision.Id;
        prompt.RequestedRevisionNumber = revision.RevisionNumber;
        prompt.RequestedAssetId = sourceAssetId;
        prompt.RequestedWorkJobId = jobId;
        prompt.UpdatedAt = revision.CreatedAt;
        db.Add(new ArtRegenerationRecord
        {
            Id = Guid.NewGuid(), ArtAssetId = sourceAssetId, VisualFingerprint = work.VisualFingerprint,
            RequestJson = JsonSerializer.Serialize(work), TargetProvider = work.Provider, TargetModel = work.Model,
            Status = "Processing", Reason = reason, RegenerationWorkJobId = jobId,
            CreatedAt = revision.CreatedAt, UpdatedAt = revision.CreatedAt
        });
    }

    private static async Task<IResult> PreviewAsync(ArtPromptReviewRequest request, SceneArtCatalog catalog,
        FatewakeDbContext db, CancellationToken ct)
    {
        var validation = Validate(request, catalog);
        if (validation is not null)
            return Results.BadRequest(new
            {
                error = validation
            });
        var asset = await db.ArtAssets.AsNoTracking().SingleOrDefaultAsync(row => row.Key == request.AssetKey &&
            row.Version == request.Version && row.Status == ArtAssetStatus.Approved, ct);
        if (asset is null)
            return Results.NotFound();
        var prompt = await FindPromptForAssetAsync(db, catalog, request.AssetKey, asset.Id, ct);
        if (prompt?.LatestRevisionId is null || prompt.Status != "ready")
            return Results.NotFound();
        var metadata = catalog.Describe(request.AssetKey, prompt.ReachedVisualContext);
        if (prompt.LatestRevisionId != request.ExpectedRevisionId)
            return Results.Conflict(new
            {
                error = "The prompt changed. Reload the selected layer before previewing."
            });
        var revision = new ArtPromptRevisionRecord
        {
            Id = Guid.NewGuid(),
            ArtPromptId = prompt.Id,
            RevisionNumber = (prompt.LatestRevisionNumber ?? 0) + 1,
            ParentRevisionId = prompt.LatestRevisionId,
            PositivePrompt = request.PositivePrompt.Trim(),
            NegativePrompt = string.IsNullOrWhiteSpace(request.NegativePrompt) ? null : request.NegativePrompt.Trim(),
            Source = "admin",
            RequestContextFingerprint = metadata.ContextFingerprint
        };
        var work = SceneArtRevision.Create(catalog, request.AssetKey, revision,
            reachedVisualContext: prompt.ReachedVisualContext);
        return Results.Ok(new ArtPromptReviewSaveResult(revision.Id, revision.RevisionNumber,
            work.ResolvedPrompt, work.NegativeInstructions ?? string.Empty, false, false, null));
    }

    private static async Task<ArtPromptRecord?> FindPromptForAssetAsync(FatewakeDbContext db,
        SceneArtCatalog catalog, string assetKey, Guid assetId, CancellationToken ct)
    {
        if (!SceneArtContextPolicy.RequiresReachedOutcome(assetKey))
        {
            var metadata = catalog.Describe(assetKey);
            return await db.Set<ArtPromptRecord>().AsNoTracking()
                .SingleOrDefaultAsync(row => row.IdentityKey == metadata.IdentityKey, ct);
        }
        var revisionId = await db.ArtGenerations.AsNoTracking()
            .Where(row => row.ArtAssetId == assetId)
            .OrderByDescending(row => row.CreatedAt)
            .Select(row => row.PromptRevisionId)
            .FirstOrDefaultAsync(ct);
        if (revisionId is null)
            return null;
        var revision = await db.Set<ArtPromptRevisionRecord>().AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == revisionId, ct);
        if (revision is null)
            return null;
        var prompt = await db.Set<ArtPromptRecord>().AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == revision.ArtPromptId, ct);
        return prompt?.ReachedVisualContext is null ? null : prompt;
    }

    private static string? Validate(ArtPromptReviewRequest request, SceneArtCatalog catalog)
    {
        if (string.IsNullOrWhiteSpace(request.SceneKey) || request.SceneKey.Length > 100 ||
            !catalog.Contains(request.AssetKey) || request.Version < 1 || request.ExpectedRevisionId == Guid.Empty)
            return "Select a valid scene layer and approved version.";
        if (string.IsNullOrWhiteSpace(request.PositivePrompt) || request.PositivePrompt.Length > 8000 ||
            request.PositivePrompt.Any(char.IsControl) || request.PositivePrompt.Contains("art/", StringComparison.OrdinalIgnoreCase) ||
            request.PositivePrompt.Contains("art\\", StringComparison.OrdinalIgnoreCase) ||
            request.PositivePrompt.Contains("```", StringComparison.Ordinal))
            return "The positive prompt must be nonempty bounded subject text.";
        if (request.NegativePrompt?.Length > 2000 || request.NegativePrompt?.Any(char.IsControl) == true ||
            request.NegativePrompt?.Contains("art/", StringComparison.OrdinalIgnoreCase) == true ||
            request.NegativePrompt?.Contains("art\\", StringComparison.OrdinalIgnoreCase) == true)
            return "The negative prompt exceeds its limit or contains invalid text.";
        return null;
    }

    private static ArtReviewRecord CreateReview(Guid account, ArtPromptReviewRequest request, Guid assetId,
        Guid revisionId, Guid? workJobId) => new()
        {
            Id = Guid.NewGuid(),
            AccountId = account,
            SceneKey = request.SceneKey,
            AssetKey = request.AssetKey,
            Version = request.Version,
            CompositionVersions = JsonSerializer.Serialize(new Dictionary<string, int?> { [request.AssetKey] = request.Version }),
            Critique = "Prompt revision saved",
            Regenerate = request.Regenerate,
            SourceArtAssetId = assetId,
            PromptRevisionId = revisionId,
            RegenerationWorkJobId = workJobId,
            CreatedAt = DateTimeOffset.UtcNow
        };

    private static async Task LockPromptAsync(FatewakeDbContext db, Guid promptId, CancellationToken ct)
    {
        var lockKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(promptId.ToString("N"))));
        var advisoryKey = "art-prompt-revision:" + lockKey;
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({advisoryKey}, 0))", ct);
    }

    private static async Task<string?> ReadReplacementStatusAsync(FatewakeDbContext db, Guid workJobId,
        CancellationToken ct)
    {
        var status = await db.WorkJobs.AsNoTracking().Where(row => row.Id == workJobId)
            .Select(row => (WorkJobStatus?)row.Status).SingleOrDefaultAsync(ct);
        return status switch
        {
            WorkJobStatus.Completed => "succeeded",
            WorkJobStatus.Failed or WorkJobStatus.Cancelled => "failed",
            null => "unavailable",
            _ => "pending"
        };
    }

    private sealed class PromptRevisionConflictException : Exception
    {
    }
}
