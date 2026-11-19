namespace Fatewake.Infrastructure.Persistence;

/// <summary>Defines bounded upload and request sizes for RAG document ingestion.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/RagIngestionLimits.md">RagIngestionLimits documentation</see>.</remarks>
public static class RagIngestionLimits
{
    /// <summary>Caps the document itself, excluding multipart metadata.</summary>
    public const long MaxDocumentBytes = 100 * 1024 * 1024;

    /// <summary>Allows multipart boundaries and bounded metadata in addition to the document.</summary>
    public const long MaxRequestBytes = MaxDocumentBytes + 1024 * 1024;
}
