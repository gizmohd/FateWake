namespace Fatewake.Infrastructure.Art;
/// <summary>Selects the configured master-art generation provider.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Art/ArtGenerationProviderOptions.md">ArtGenerationProviderOptions documentation</see>.</remarks>
public sealed class ArtGenerationProviderOptions
{
 public const string SectionName="ArtGeneration";
 public string Provider{get;set;}="None";
}