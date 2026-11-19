using Fatewake.Infrastructure.Art;
using Fatewake.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fatewake.Api;

/// <summary>Resolves prompts from SQL and durably coalesces missing-prompt authoring.</summary>
/// <see href="../../../docs/code/src/Fatewake.Api/SceneArtPromptService.md">SceneArtPromptService documentation</see>
public sealed class SceneArtPromptService(FatewakeDbContext db, SceneArtCatalog catalog,
    ArtPromptJobFactory jobs, IConfiguration configuration, ILogger<SceneArtPromptService> log)
{
    /// <summary>Returns a ready request or persists an explicit pending, disabled, or failed state.</summary>
    public async Task<SceneArtPromptResolution> ResolveAsync(string key, Guid? accountId, Guid? survivorId,
        int priority = 0, CancellationToken ct = default, string? reachedVisualContext = null)
    {
        if (SceneArtContextPolicy.RequiresReachedOutcome(key) &&
            !SceneArtContextPolicy.IsCanonicalVisualContext(reachedVisualContext))
            throw new ArgumentException("A supported reached outcome is required for this scene layer.", nameof(reachedVisualContext));
        var metadata = catalog.Describe(key, reachedVisualContext);
        var stateWithReplacement = await db.Set<ArtPromptRecord>().AsNoTracking()
            .Where(row => row.IdentityKey == metadata.IdentityKey)
            .Select(row => new
            {
                Prompt = row,
                ReplacementPending = row.RequestedWorkJobId != null && db.WorkJobs.Any(job =>
                    job.Id == row.RequestedWorkJobId && (job.Status == WorkJobStatus.Pending || job.Status == WorkJobStatus.Running))
            }).SingleOrDefaultAsync(ct);
        var existing = stateWithReplacement?.Prompt;
        if (existing is { Status: "ready", CurrentRevisionId: { } revisionId })
        {
            var revision = await db.Set<ArtPromptRevisionRecord>().AsNoTracking()
                .SingleOrDefaultAsync(row => row.Id == revisionId && row.ArtPromptId == existing.Id, ct);
            if (revision is null)
                throw new InvalidDataException("The current SQL artwork prompt revision is missing.");
            return new("ready", catalog.CreateWorkRequest(metadata, revision,
                existing.IsPrivate ? accountId : null, existing.IsPrivate ? survivorId : null, priority),
                ReplacementPending: stateWithReplacement!.ReplacementPending);
        }
        var authoringEnabled = configuration.GetValue<bool>(ArtPromptAuthoringOptions.SectionName + ":Enabled") &&
            !string.IsNullOrWhiteSpace(configuration[ArtPromptAuthoringOptions.SectionName + ":ApiKey"]);
        if (!authoringEnabled && reachedVisualContext is not null &&
            (existing is null || existing.Status == "disabled") &&
            await TryUseContextualSeedAsync(metadata, ct))
            return await ResolveAsync(key, accountId, survivorId, priority, ct, reachedVisualContext);

        if (existing is not null && (existing.Status != "disabled" || !authoringEnabled))
            return new(existing.Status is "pending" or "disabled" or "failed" ? existing.Status : "failed",
                JobId: existing.AuthoringJobId);

        if (!authoringEnabled)
        {
            await StoreDisabledAsync(metadata, ct);
            return new("disabled");
        }

        var snapshot = catalog.CreateAuthoringRequest(metadata, accountId, survivorId) with
        {
            VisualPreferences = await ArtReviewPreferences.LoadAsync(db, key, ct)
        };
        var job = await jobs.CreateAsync(snapshot, async (jobId, token) =>
        {
            var prompt = await db.Set<ArtPromptRecord>().SingleOrDefaultAsync(
                row => row.IdentityKey == metadata.IdentityKey, token);
            if (prompt is null)
            {
                var now = DateTimeOffset.UtcNow;
                prompt = NewPrompt(metadata, now);
                prompt.AuthoringJobId = jobId;
                db.Add(prompt);
            }
            else if (prompt.Status != "ready")
            {
                prompt.Status = "pending";
                prompt.AuthoringJobId = jobId;
                prompt.FailureCode = null;
                prompt.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }, ct);
        var state = await db.Set<ArtPromptRecord>().AsNoTracking()
            .SingleAsync(row => row.IdentityKey == metadata.IdentityKey, ct);
        log.LogInformation("Queued artwork prompt state {PromptStatus} for allowlisted layer {AssetKey}",
            state.Status, metadata.AssetKey);
        return state.Status == "ready"
            ? await ResolveAsync(key, accountId, survivorId, priority, ct, reachedVisualContext)
            : new(state.Status, JobId: job.JobId);
    }

    private async Task<bool> TryUseContextualSeedAsync(SceneArtPromptMetadata metadata, CancellationToken ct)
    {
        var baseIdentity = catalog.Describe(metadata.AssetKey).IdentityKey;
        var seed = await (from prompt in db.Set<ArtPromptRecord>().AsNoTracking()
                          join baseRevision in db.Set<ArtPromptRevisionRecord>().AsNoTracking()
                              on prompt.Id equals baseRevision.ArtPromptId
                          where prompt.IdentityKey == baseIdentity && baseRevision.Source == "seed"
                          orderby baseRevision.RevisionNumber descending
                          select baseRevision).FirstOrDefaultAsync(ct);
        if (seed is null)
            return false;

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockPromptIdentityAsync(metadata.IdentityKey, ct);
        var current = await db.Set<ArtPromptRecord>().SingleOrDefaultAsync(row => row.IdentityKey == metadata.IdentityKey, ct);
        if (current is { Status: "ready", CurrentRevisionId: not null })
            return true;
        if (current is not null && current.Status != "disabled")
            return false;

        var now = DateTimeOffset.UtcNow;
        if (current is null)
        {
            current = NewPrompt(metadata, now);
            db.Add(current);
        }

        var revision = new ArtPromptRevisionRecord
        {
            Id = Guid.NewGuid(),
            ArtPromptId = current.Id,
            RevisionNumber = (current.LatestRevisionNumber ?? 0) + 1,
            ParentRevisionId = current.LatestRevisionId,
            PositivePrompt = seed.PositivePrompt,
            NegativePrompt = seed.NegativePrompt,
            Source = "contextual-seed",
            RequestContextFingerprint = metadata.ContextFingerprint,
            CreatedAt = now
        };
        db.Add(revision);
        await db.SaveChangesAsync(ct);

        current.Status = "ready";
        current.CurrentRevisionId = current.LatestRevisionId = revision.Id;
        current.CurrentRevisionNumber = current.LatestRevisionNumber = revision.RevisionNumber;
        current.FailureCode = null;
        current.AuthoringJobId = null;
        current.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return true;
    }

    private Task LockPromptIdentityAsync(string identity, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({"art.prompt:" + identity}, 0))", ct);

    private async Task StoreDisabledAsync(SceneArtPromptMetadata metadata, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockPromptIdentityAsync(metadata.IdentityKey, ct);
        var prompt = await db.Set<ArtPromptRecord>()
            .SingleOrDefaultAsync(row => row.IdentityKey == metadata.IdentityKey, ct);
        if (prompt is null)
        {
            var now = DateTimeOffset.UtcNow;
            prompt = NewPrompt(metadata, now);
            prompt.Status = "disabled";
            prompt.FailureCode = "authoring-disabled";
            db.Add(prompt);
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
    }

    private static ArtPromptRecord NewPrompt(SceneArtPromptMetadata metadata, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        IdentityKey = metadata.IdentityKey,
        AssetKey = metadata.AssetKey,
        ContextFingerprint = metadata.ContextFingerprint,
        StyleBibleVersion = metadata.StyleBibleVersion,
        ReachedVisualContext = metadata.ReachedVisualContext,
        CreatedAt = now,
        UpdatedAt = now
    };
}
