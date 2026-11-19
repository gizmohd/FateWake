namespace Fatewake.Web.Presentation;

/// <summary>API response for a completed RAG document import.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Web/Presentation/RagImportResultDto.md">RagImportResultDto documentation</see>.</remarks>
public sealed record RagImportResultDto(Guid WorkJobId, Guid StagingId, bool ReusedJob);
