namespace Fatewake.Infrastructure.Art;
/// <summary>Configures a native ComfyUI master-art generator and its API workflow template.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Art/ComfyUiArtGenerationOptions.md">ComfyUiArtGenerationOptions documentation</see>.</remarks>
public sealed class ComfyUiArtGenerationOptions
{
 public const string SectionName="ArtGeneration:ComfyUI";
 public bool Enabled{get;set;}
 public string BaseUrl{get;set;}="http://127.0.0.1:8188/";
 public string WorkflowPath{get;set;}="workflows/fatewake-api.json";
 public string PromptToken{get;set;}="__FATEWAKE_PROMPT__";
 public string NegativePromptToken{get;set;}="__FATEWAKE_NEGATIVE__";
 public string SeedToken{get;set;}="__FATEWAKE_SEED__";
 public string ReferenceTokenPrefix{get;set;}="__FATEWAKE_REFERENCE_";
 public int PollIntervalMilliseconds{get;set;}=1000;
 public int TimeoutSeconds{get;set;}=300;
}