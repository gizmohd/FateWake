using System.Security.Cryptography;
using System.Text;
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
                prompt = new ArtPromptRecord
                {
                    Id = Guid.NewGuid(),
                    IdentityKey = metadata.IdentityKey,
                    AssetKey = metadata.AssetKey,
                    ContextFingerprint = metadata.ContextFingerprint,
                    StyleBibleVersion = metadata.StyleBibleVersion,
                    ReachedVisualContext = metadata.ReachedVisualContext,
                    Status = "pending",
                    AuthoringJobId = jobId,
                    CreatedAt = now,
                    UpdatedAt = now
                };
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

    private async Task StoreDisabledAsync(SceneArtPromptMetadata metadata, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var lockKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(metadata.IdentityKey)));
        var advisoryKey = "art-prompt:" + lockKey;
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({advisoryKey}, 0))", ct);
        var prompt = await db.Set<ArtPromptRecord>()
            .SingleOrDefaultAsync(row => row.IdentityKey == metadata.IdentityKey, ct);
        if (prompt is null)
        {
            var now = DateTimeOffset.UtcNow;
            db.Add(new ArtPromptRecord
            {
                Id = Guid.NewGuid(),
                IdentityKey = metadata.IdentityKey,
                AssetKey = metadata.AssetKey,
                ContextFingerprint = metadata.ContextFingerprint,
                StyleBibleVersion = metadata.StyleBibleVersion,
                ReachedVisualContext = metadata.ReachedVisualContext,
                Status = "disabled",
                FailureCode = "authoring-disabled",
                CreatedAt = now,
                UpdatedAt = now
            });
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
    }
}
