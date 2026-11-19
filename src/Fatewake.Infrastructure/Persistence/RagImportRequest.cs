namespace Fatewake.Infrastructure.Persistence;

/// <summary>Describes one uploaded RAG source and its optional administrative metadata.</summary>
public sealed record RagImportRequest(
    string FileName,
    string ContentType,
    byte[] Bytes,
    string SourceKey,
    string SourceType,
    string Title,
    string LicenseStatus,
    string ReviewStatus,
    string Visibility,
    string? TimelineKey,
    int? SpoilerSeries);
