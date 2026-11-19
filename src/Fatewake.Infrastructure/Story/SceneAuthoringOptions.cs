namespace Fatewake.Infrastructure.Story;

/// <summary>Controls AI scene authoring; disabled by default so no story is generated without an explicit key.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Story/SceneAuthoringOptions.md">Documentation</see>.</remarks>
public sealed class SceneAuthoringOptions
{
    /// <summary>Gets the configuration section name shared by API and worker hosts.</summary>
    public const string SectionName = "SceneAuthoring";
    /// <summary>Gets or sets whether scene generation is enabled.</summary>
    public bool Enabled
    {
        get; set;
    }
    /// <summary>Gets or sets the protected OpenAI API credential.</summary>
    public string? ApiKey
    {
        get; set;
    }
    /// <summary>Gets or sets the OpenAI Responses base URI.</summary>
    public string BaseUrl { get; set; } = "https://api.openai.com/v1/";
    /// <summary>Gets or sets the narrator model.</summary>
    public string Model { get; set; } = "gpt-6-luna";
    /// <summary>Gets or sets the reasoning effort.</summary>
    public string ReasoningEffort { get; set; } = "low";
    /// <summary>Gets or sets the HTTP timeout in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 120;
    /// <summary>Gets or sets the bounded output-token budget.</summary>
    public int MaximumOutputTokens { get; set; } = 4000;
    /// <summary>Gets or sets how many canon passages are retrieved per scene.</summary>
    public int RetrievalLimit { get; set; } = 8;
}
