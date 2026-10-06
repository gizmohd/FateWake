namespace Fatewake.GameEngine;

/// <summary>
/// Represents a normalized player action submitted to the deterministic game engine.
/// </summary>
/// <param name="ActionType">Stable identifier for the requested action.</param>
/// <param name="Arguments">Structured arguments used to resolve the action.</param>
/// <param name="RawInput">Optional original free-form input, if the action was derived from text.</param>
/// <see href="../../docs/code/src/Fatewake.GameEngine/CandidateAction.md">CandidateAction documentation</see>
public sealed record CandidateAction(
    string ActionType,
    IReadOnlyDictionary<string, string> Arguments,
    string? RawInput = null);
