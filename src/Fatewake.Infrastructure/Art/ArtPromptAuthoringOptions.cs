namespace Fatewake.Infrastructure.Art;

/// <summary>Controls optional presentation-only authoring of missing artwork prompts.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Art/ArtPromptAuthoringOptions.md">Documentation</see>.</remarks>
public sealed class ArtPromptAuthoringOptions
{
    /// <summary>Gets the configuration section name shared by API and worker hosts.</summary>
    public const string SectionName = "ArtPromptAuthoring";
    /// <summary>Gets or sets whether durable missing-prompt authoring is enabled.</summary>
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
    /// <summary>Gets or sets the configurable text-generation model.</summary>
    public string Model { get; set; } = "gpt-6-luna";
    /// <summary>Gets or sets the reasoning effort; `none` is required for this bounded task.</summary>
    public string ReasoningEffort { get; set; } = "none";
    /// <summary>Gets or sets the HTTP timeout in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 90;
    /// <summary>Gets or sets the bounded Responses output-token budget.</summary>
    public int MaximumOutputTokens { get; set; } = 1200;
}
