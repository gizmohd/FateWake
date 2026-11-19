using Fatewake.Infrastructure.Persistence;
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
        group.MapPost("/import", async (
            HttpRequest request,
            IRagStagingStore staging,
            IWorkJobBuilder jobs,
            CancellationToken ct) =>
        {
            if (!request.HasFormContentType)
                return Results.BadRequest(new { error = "A multipart document upload is required." });
            var form = await request.ReadFormAsync(ct);
            var file = form.Files.GetFile("file");
            if (file is null || file.Length is < 1 or > RagIngestionLimits.MaxDocumentBytes)
                return Results.BadRequest(new { error = "Choose a non-empty document no larger than 100 MiB." });
            var sourceKey = form["sourceKey"].ToString().Trim();
            var title = form["title"].ToString().Trim();
            if (sourceKey.Length is < 1 or > 200 || title.Length is < 1 or > 300)
                return Results.BadRequest(new { error = "Source key and title are required." });
            await using var stream = file.OpenReadStream();
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, ct);
                var bytes = buffer.ToArray();
                var contentHash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
                var staged = await staging.PutAsync(
                    contentHash,
                    file.FileName,
                    file.ContentType ?? "application/octet-stream",
                    bytes,
                    ct);
                var workRequest = new RagWorkRequest(
                    staged.Id,
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
                return Results.Accepted("/api/admin/rag/status", new RagQueuedResult(created.JobId, staged.Id, created.Existing));
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
