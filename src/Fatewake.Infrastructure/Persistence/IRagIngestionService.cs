namespace Fatewake.Infrastructure.Persistence;

/// <summary>Imports authorized text-bearing documents into the RAG source, document, chunk, and run tables.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/IRagIngestionService.md">IRagIngestionService documentation</see>.</remarks>
public interface IRagIngestionService
{
    Task<RagImportResult> ImportAsync(RagImportRequest request, CancellationToken ct = default);
    Task<RagStatusResult> StatusAsync(CancellationToken ct = default);
}
