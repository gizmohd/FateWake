namespace Fatewake.Infrastructure.Art;
/// <summary>Configures a local OpenAI-compatible image generation endpoint, including middleware backed by local models.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Art/LocalArtGenerationOptions.md">LocalArtGenerationOptions documentation</see>.</remarks>
public sealed class LocalArtGenerationOptions
{
 public const string SectionName="ArtGeneration:Local";
 public bool Enabled{get;set;}
 public string BaseUrl{get;set;}="http://localhost:11434/v1/";
 public string? ApiKey{get;set;}
 public string Model{get;set;}="local-image";
 public string Size{get;set;}="1536x1024";
 public string Quality{get;set;}="high";
}