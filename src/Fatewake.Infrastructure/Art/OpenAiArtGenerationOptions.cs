namespace Fatewake.Infrastructure.Art;
/// <summary>Configures OpenAI GPT Image generation without embedding credentials in source.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Art/OpenAiArtGenerationOptions.md">OpenAiArtGenerationOptions documentation</see>.</remarks>
public sealed class OpenAiArtGenerationOptions
{
 public const string SectionName="ArtGeneration:OpenAI";
 public bool Enabled{get;set;}
 public string? ApiKey{get;set;}
 public string BaseUrl{get;set;}="https://api.openai.com/v1/";
 public string Model{get;set;}="gpt-image-2.5-sunburst";
 public string Size{get;set;}="1536x1024";
 public string Quality{get;set;}="high";
 public string Background{get;set;}="opaque";
}