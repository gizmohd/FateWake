using System.Text.Json;
using Fatewake.Infrastructure.Work;

namespace Fatewake.Infrastructure.Persistence;

/// <summary>Processes a staged RAG document through the ingestion service in a durable Worker lease.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/RagImportWorkStepHandler.md">RagImportWorkStepHandler documentation</see>.</remarks>
public sealed class RagImportWorkStepHandler(
    IRagStagingStore staging,
    IRagIngestionService ingestion) : IWorkStepHandler
{
    public string StepType => RagWorkStepTypes.Import;

    public async Task<string?> ExecuteAsync(WorkStepExecutionContext context, CancellationToken ct)
    {
        var request = JsonSerializer.Deserialize<RagWorkRequest>(context.Input)
            ?? throw new InvalidOperationException("Invalid RAG import work request.");
        var staged = await staging.GetAsync(request.StagingId, ct)
            ?? throw new InvalidOperationException("RAG staged upload was not found.");
        var result = await ingestion.ImportAsync(new RagImportRequest(
            staged.FileName,
            staged.ContentType,
            staged.Bytes,
            request.SourceKey,
            request.SourceType,
            request.Title,
            request.LicenseStatus,
            request.ReviewStatus,
            request.Visibility,
            request.TimelineKey,
            request.SpoilerSeries), ct);
        await staging.DeleteAsync(request.StagingId, ct);
        return JsonSerializer.Serialize(result);
    }
}
