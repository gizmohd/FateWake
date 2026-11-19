namespace Fatewake.Api;

/// <summary>Player decision for the current generated scene.</summary>
/// <remarks><see href="../../docs/code/src/Fatewake.Api/StoryChoiceRequest.md">StoryChoiceRequest documentation</see>.</remarks>
/// <param name="ActionType">Offered action type, or <c>freeform</c>.</param>
/// <param name="RawInput">Free text for freeform actions.</param>
public sealed record StoryChoiceRequest(string ActionType, string? RawInput);
