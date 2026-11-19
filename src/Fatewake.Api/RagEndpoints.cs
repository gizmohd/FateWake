using System.Security.Claims;
using Fatewake.Infrastructure.Persistence;
using Fatewake.Infrastructure.Rag;
using Fatewake.Infrastructure.Work;
using System.Security.Cryptography;
using System.Text.Json;

namespace Fatewake.Api;

/// <summary>Exposes administrator-only RAG corpus upload and status operations.</summary>
/// <see href="../../docs/code/src/Fatewake.Api/RagEndpoints.md">Documentation</see>
public static class RagEndpoints
{
    /// <summary>Maps multipart imports and database metrics to the protected RAG administration surface.</summary>
    public static void MapRagAdmin(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin/rag").RequireAuthorization("ArtAdministrator");
        group.MapGet("/status", async (IRagIngestionService rag, CancellationToken ct) =>
            Results.Ok(await rag.StatusAsync(ct)));
        group.MapGet("/claims", async (string? status, int? page, RagClaimService claims, CancellationToken ct) =>
        {
            status ??= RagClaimService.Pending;
            if (!RagClaimService.IsReviewStatus(status) || page is < 1 or > 100_000)
                return Results.BadRequest(new { error = "Choose a valid review status and page." });
            return Results.Ok(await claims.ListAsync(status, page ?? 1, ct));
        });
        group.MapPut("/claims/{id:guid}/decision", async (Guid id, RagClaimDecision decision, ClaimsPrincipal user,
            RagClaimService claims, CancellationToken ct) =>
        {
            if (!RagClaimService.IsReviewStatus(decision.ExpectedStatus) ||
                decision.Status is not (RagClaimService.Approved or RagClaimService.Rejected) ||
                decision.Status == decision.ExpectedStatus)
                return Results.BadRequest(new { error = "Choose a valid claim decision." });
            if (!Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var reviewer))
                return Results.Forbid();
            return await claims.ReviewAsync(id, decision, reviewer.ToString(), ct) == 1
                ? Results.NoContent()
                : Results.Conflict(new { error = "The claim changed or its source is no longer eligible. Refresh the list." });
        });
        group.MapGet("/claim-sources", async (string? search, RagClaimService claims, CancellationToken ct) =>
        {
            if (search?.Length > 200)
                return Results.BadRequest(new { error = "Use a shorter source search." });
            return Results.Ok(await claims.ListSourcesAsync(search, ct));
        });
        group.MapPut("/claim-sources/{id:guid}/policy", async (Guid id, RagClaimSourcePolicy policy,
            RagClaimService claims, CancellationToken ct) =>
        {
            var minimum = policy.MinimumConfidence;
            if (minimum is < 0 or > 1 || (minimum is { } value && decimal.Round(value, 4) != value))
                return Results.BadRequest(new { error = "Enter a confidence from 0 to 1 with at most four decimal places." });
            return await claims.SetPolicyAsync(id, minimum, ct)
                ? Results.NoContent()
                : Results.NotFound(new { error = "Choose an eligible authorized source." });
        });
        group.MapPost("/import", async (
            HttpRequest request,
            IRagStagingStore staging,
            IRagIngestionService ingestion,
            IWorkJobBuilder jobs,
            CancellationToken ct) =>
        {
            if (!request.HasFormContentType)
                return Results.BadRequest(new
                {
                    error = "A multipart document upload is required."
                });
            var form = await request.ReadFormAsync(ct);
            var file = form.Files.GetFile("file");
            if (file is null || file.Length is < 1 or > RagIngestionLimits.MaxDocumentBytes)
                return Results.BadRequest(new
                {
                    error = "Choose a non-empty document no larger than 100 MiB."
                });
            var sourceKey = form["sourceKey"].ToString().Trim();
            var title = form["title"].ToString().Trim();
            if (sourceKey.Length is < 1 or > 200 || title.Length is < 1 or > 300)
                return Results.BadRequest(new
                {
                    error = "Source key and title are required."
                });
            await using var stream = file.OpenReadStream();
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, ct);
            var bytes = buffer.ToArray();
            var contentHash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            var stagingId = await staging.PutAsync(
                sourceKey,
                contentHash,
                file.FileName,
                file.ContentType ?? "application/octet-stream",
                bytes,
                ct);
            var workRequest = new RagWorkRequest(
                stagingId,
                file.FileName,
                file.ContentType ?? "application/octet-stream",
                sourceKey,
                First(form, "sourceType", "Manuscript"),
                title,
                First(form, "licenseStatus", "Authorized"),
                First(form, "reviewStatus", "PendingReview"),
                First(form, "visibility", "Internal"),
                NullIfBlank(form["timelineKey"].ToString()),
                int.TryParse(form["spoilerSeries"].ToString(), out var spoiler) ? spoiler : null,
                contentHash);
            var payload = JsonSerializer.Serialize(workRequest);
            var created = await jobs.CreateAsync(new WorkJobDefinition(
                "rag.import",
                $"rag:{contentHash}:{sourceKey}",
                payload,
                [new WorkStepDefinition("ingest", RagWorkStepTypes.Import, RagWorkQueues.Import, payload)]),
                ct);
            if (created.Existing && await ingestion.FindCompletedAsync(sourceKey, contentHash, ct) is not null)
                await staging.DeleteAsync(stagingId, ct);
            return Results.Accepted("/api/admin/rag/status", new RagQueuedResult(created.JobId, stagingId, created.Existing));
        }).DisableAntiforgery();
    }

    private static string First(IFormCollection form, string key, string fallback)
    {
        var value = form[key].ToString().Trim();
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static string? NullIfBlank(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

}
