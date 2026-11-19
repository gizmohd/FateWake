namespace Fatewake.Infrastructure.Art;

/// <summary>Contains validated presentation text and safe provider usage metadata.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Art/ArtPromptAuthoringResult.md">Documentation</see>.</remarks>
/// <param name="PositivePrompt">Validated positive subject text.</param>
/// <param name="NegativePrompt">Optional validated negative subject text.</param>
/// <param name="ProviderRequestId">Provider request identity, if returned.</param>
/// <param name="InputTokens">Provider-reported input token count.</param>
/// <param name="OutputTokens">Provider-reported output token count.</param>
public sealed record ArtPromptAuthoringResult(string PositivePrompt, string? NegativePrompt,
    string? ProviderRequestId, long? InputTokens, long? OutputTokens);
