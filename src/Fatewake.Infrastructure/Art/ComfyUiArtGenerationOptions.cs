namespace Fatewake.Infrastructure.Art;
/// <summary>Configures a native ComfyUI master-art generator and its API workflow template.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Art/ComfyUiArtGenerationOptions.md">ComfyUiArtGenerationOptions documentation</see>.</remarks>
public sealed class ComfyUiArtGenerationOptions
{
    public const string SectionName = "ArtGeneration:ComfyUI";
    /// <summary>Optional API-workflow token replaced with the model filename from the artwork request.</summary>
    public const string ModelToken = "__FATEWAKE_MODEL__";
    public bool Enabled { get; set; }
    /// <summary>History route relative to BaseUrl: history locally or history_v2 on Comfy Cloud.</summary>
    public string HistoryPath { get; set; } = "history";
    public string BaseUrl { get; set; } = "http://127.0.0.1:8188/";
    /// <summary>Optional key for Comfy Cloud or an authenticating reverse proxy. Null, empty, or whitespace means not configured and is normalized to <see langword="null"/>; no authentication header is sent.</summary>
    public string? ApiKey { get; set => field = string.IsNullOrWhiteSpace(value) ? null : value; }
    /// <summary>Header carrying <see cref="ApiKey"/>; <c>X-API-Key</c> for Comfy Cloud, or <c>Authorization</c> to send a bearer token.</summary>
    public string ApiKeyHeader { get; set; } = "X-API-Key";
    public string WorkflowPath { get; set; } = "workflows/fatewake-api.json";
    /// <summary>Optional alpha-producing workflow for reusable non-background layers.</summary>
    public string? OverlayWorkflowPath { get; set; }
    public string PromptToken { get; set; } = "__FATEWAKE_PROMPT__";
    public string NegativePromptToken { get; set; } = "__FATEWAKE_NEGATIVE__";
    public string SeedToken { get; set; } = "__FATEWAKE_SEED__";
    public string ReferenceTokenPrefix { get; set; } = "__FATEWAKE_REFERENCE_";
    public int PollIntervalMilliseconds { get; set; } = 1000;
    public int TimeoutSeconds { get; set; } = 300;
}
