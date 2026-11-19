namespace Fatewake.Infrastructure.Rag;

/// <summary>One retrieved passage with provenance and relevance.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Rag/RagPassage.md">Documentation</see>.</remarks>
public sealed class RagPassage
{
    /// <summary>Gets or sets the chunk identifier.</summary>
    public Guid ChunkId
    {
        get; set;
    }
    /// <summary>Gets or sets the owning source key.</summary>
    public string SourceKey { get; set; } = "";
    /// <summary>Gets or sets the owning source type.</summary>
    public string SourceType { get; set; } = "";
    /// <summary>Gets or sets the source title.</summary>
    public string Title { get; set; } = "";
    /// <summary>Gets or sets the passage text.</summary>
    public string Text { get; set; } = "";
    /// <summary>Gets or sets relevance; higher is better, comparable only within one result set.</summary>
    public double Score
    {
        get; set;
    }
}
