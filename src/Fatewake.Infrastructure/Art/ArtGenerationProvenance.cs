namespace Fatewake.Infrastructure.Art;

/// <summary>Captures the provider request details needed to audit and reproduce image generation.</summary>
/// <param name="Operation">Image-generation or editing operation.</param>
/// <param name="PromptKey">Optional stable key for the authored prompt template.</param>
/// <param name="PromptTemplateVersion">Optional immutable prompt-template revision.</param>
/// <param name="ResolvedPrompt">Exact prompt submitted to the provider.</param>
/// <param name="NegativePrompt">Optional provider negative prompt.</param>
/// <param name="Provider">Provider identifier.</param>
/// <param name="Model">Provider model identifier.</param>
/// <param name="ProviderJobId">Optional provider-side job identifier.</param>
/// <param name="PromptBuilderVersion">Optional prompt-construction code version.</param>
/// <param name="StyleBibleVersion">Optional visual style revision.</param>
/// <param name="ReferenceAssets">Stable identities/versions of reference art.</param>
/// <param name="GenerationParameters">Provider-supported generation settings.</param>
/// <param name="ContinuityVersions">Character/location continuity reference versions.</param>
/// <param name="CostUsd">Optional recorded provider cost.</param>
/// <see href="../../../docs/code/src/Fatewake.Infrastructure/Art/ArtGenerationProvenance.md">ArtGenerationProvenance documentation</see>
public sealed record ArtGenerationProvenance(
    string Operation,
    string? PromptKey,
    string? PromptTemplateVersion,
    string ResolvedPrompt,
    string? NegativePrompt,
    string Provider,
    string Model,
    string? ProviderJobId,
    string? PromptBuilderVersion,
    string? StyleBibleVersion,
    IReadOnlyList<string> ReferenceAssets,
    IReadOnlyDictionary<string, object?> GenerationParameters,
    IReadOnlyDictionary<string, string> ContinuityVersions,
    decimal? CostUsd);
