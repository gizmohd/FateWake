namespace Fatewake.Infrastructure.Rag;

/// <summary>Configures optional OpenAI embeddings that make RAG chunks retrievable by meaning.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Rag/EmbeddingOptions.md">Documentation</see>.</remarks>
public sealed class EmbeddingOptions
{
    /// <summary>Gets the configuration section name shared by API and worker hosts.</summary>
    public const string SectionName = "Embedding";
    /// <summary>Gets or sets whether chunk embedding and semantic retrieval are enabled.</summary>
    public bool Enabled
    {
        get; set;
    }
    /// <summary>Gets or sets the protected OpenAI API credential.</summary>
    public string? ApiKey
    {
        get; set;
    }
    /// <summary>Gets or sets the OpenAI base URI.</summary>
    public string BaseUrl { get; set; } = "https://api.openai.com/v1/";
    /// <summary>Gets or sets the embedding model.</summary>
    public string Model { get; set; } = "text-embedding-3-small";
    /// <summary>Gets or sets the vector size; it must match the <c>rag_chunk.Embedding</c> column.</summary>
    public int Dimensions { get; set; } = 1024;
    /// <summary>Gets or sets the number of chunks embedded per provider call.</summary>
    public int BatchSize { get; set; } = 32;
    /// <summary>Gets or sets the HTTP timeout in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 60;
    /// <summary>Gets or sets the idle delay between backfill polls in seconds.</summary>
    public int PollIntervalSeconds { get; set; } = 10;
}
