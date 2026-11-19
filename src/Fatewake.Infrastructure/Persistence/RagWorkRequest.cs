namespace Fatewake.Infrastructure.Persistence;

/// <summary>Names a staged RAG upload and the metadata required by the Worker ingestion handler.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/RagWorkRequest.md">RagWorkRequest documentation</see>.</remarks>
public sealed record RagWorkRequest(
    Guid StagingId,
    string FileName,
    string ContentType,
    string SourceKey,
    string SourceType,
    string Title,
    string LicenseStatus,
    string ReviewStatus,
    string Visibility,
    string? TimelineKey,
    int? SpoilerSeries,
    string ContentHash);
