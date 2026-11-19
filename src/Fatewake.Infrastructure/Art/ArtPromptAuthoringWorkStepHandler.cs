using Microsoft.Extensions.Configuration;
using System.Text.Json;
using Fatewake.Infrastructure.Persistence;
using Fatewake.Infrastructure.Work;
using Fatewake.Observability;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fatewake.Infrastructure.Art;

/// <summary>Persists one validated prompt revision and its successor image job as an atomic work intent.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Art/ArtPromptAuthoringWorkStepHandler.md">Documentation</see>.</remarks>
public sealed class ArtPromptAuthoringWorkStepHandler(FatewakeDbContext db, IArtPromptAuthor author,
    IArtWorkJobFactory imageJobs, ArtPromptAuthoringOptions options,
    IConfiguration configuration, ILogger<ArtPromptAuthoringWorkStepHandler> log) : IWorkStepHandler
{
    /// <inheritdoc />
    public string StepType => ArtPromptWorkStepTypes.Author;

    /// <inheritdoc />
    public async Task<string?> ExecuteAsync(WorkStepExecutionContext context, CancellationToken ct)
    {
        using var operation = OperationTelemetry.Start("art.prompt.work", log);
        ArtPromptAuthoringRequest request;
        try
        {
            request = JsonSerializer.Deserialize<ArtPromptAuthoringRequest>(context.Input)
                ?? throw new InvalidDataException("Invalid artwork prompt work input.");
            var prompt = await db.Set<ArtPromptRecord>().SingleAsync(row => row.IdentityKey == request.IdentityKey, ct);
            if (prompt.Status == "ready" && prompt.CurrentRevisionId is not null)
                return JsonSerializer.Serialize(new
                {
                    status = "ready",
                    revisionId = prompt.CurrentRevisionId
                });
            if (!options.Enabled || string.IsNullOrWhiteSpace(options.ApiKey))
            {
                prompt.Status = "disabled";
                prompt.FailureCode = "authoring-disabled";
                prompt.UpdatedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(ct);
                return JsonSerializer.Serialize(new
                {
                    status = "disabled"
                });
            }

            var result = await author.AuthorAsync(request, context.JobId, context.Attempt, ct);
            if (!string.Equals(prompt.AssetKey, request.AssetKey, StringComparison.Ordinal) ||
                !string.Equals(prompt.IdentityKey, request.IdentityKey, StringComparison.Ordinal) ||
                !string.Equals(prompt.ContextFingerprint, request.ContextFingerprint, StringComparison.Ordinal) ||
                !string.Equals(prompt.StyleBibleVersion, request.StyleBibleVersion, StringComparison.Ordinal))
                throw new InvalidDataException("Artwork prompt context no longer matches its server-owned identity.");

            var revision = new ArtPromptRevisionRecord
            {
                Id = Guid.NewGuid(),
                ArtPromptId = prompt.Id,
                RevisionNumber = (prompt.CurrentRevisionNumber ?? 0) + 1,
                ParentRevisionId = prompt.CurrentRevisionId,
                PositivePrompt = result.PositivePrompt,
                NegativePrompt = result.NegativePrompt,
                Source = "ai",
                Model = options.Model,
                RequestContextFingerprint = request.ContextFingerprint,
                CreatedAt = DateTimeOffset.UtcNow
            };
            var work = ArtPromptWorkRequestBuilder.Create(configuration, request, revision,
                request.OwnerAccountId, request.SurvivorId);
            var imageJob = await imageJobs.CreateAsync(work, async (_, token) =>
            {
                var current = await db.Set<ArtPromptRecord>().SingleAsync(row => row.Id == prompt.Id, token);
                if (current.Status == "ready" && current.CurrentRevisionId is { } currentRevision &&
                    currentRevision != prompt.CurrentRevisionId)
                    throw new InvalidOperationException("The artwork prompt changed while authoring was in progress.");
                db.Add(revision);
                current.LatestRevisionId = revision.Id;
                current.LatestRevisionNumber = revision.RevisionNumber;
                current.CurrentRevisionId = revision.Id;
                current.CurrentRevisionNumber = revision.RevisionNumber;
                current.Status = "ready";
                current.FailureCode = null;
                current.AuthoringJobId = null;
                current.UpdatedAt = DateTimeOffset.UtcNow;
            }, ct);
            log.LogInformation("Stored artwork prompt revision {PromptRevisionNumber} and successor work {WorkJobId}",
                revision.RevisionNumber, imageJob.JobId);
            return JsonSerializer.Serialize(new
            {
                status = "ready",
                revisionId = revision.Id,
                imageJobId = imageJob.JobId
            });
        }
        catch (OperationCanceledException ex) when (ct.IsCancellationRequested)
        {
            operation.Fail(ex);
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            operation.Fail(ex);
            if (context.Attempt >= 3)
                await MarkTerminalAsync(context.Input, "provider-retries-exhausted", ct);
            log.LogWarning(ex, "Artwork prompt authoring attempt {WorkAttempt} failed transiently", context.Attempt);
            throw;
        }
        catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException)
        {
            operation.Fail(ex);
            await MarkTerminalAsync(context.Input, ex is InvalidDataException ? "invalid-provider-output" : "provider-rejected", ct);
            log.LogWarning(ex, "Artwork prompt authoring ended without a usable revision");
            return JsonSerializer.Serialize(new
            {
                status = "failed",
                error = "prompt-authoring-failed"
            });
        }
    }

    private async Task MarkTerminalAsync(string input, string failureCode, CancellationToken ct)
    {
        var request = JsonSerializer.Deserialize<ArtPromptAuthoringRequest>(input);
        if (request is null)
            return;
        var prompt = await db.Set<ArtPromptRecord>().SingleOrDefaultAsync(
            row => row.IdentityKey == request.IdentityKey, ct);
        if (prompt is null || prompt.Status == "ready")
            return;
        prompt.Status = "failed";
        prompt.FailureCode = failureCode;
        prompt.AuthoringJobId = null;
        prompt.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }
}
