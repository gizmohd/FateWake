namespace Fatewake.GameEngine;

/// <summary>
/// Contains the authoritative result of evaluating a candidate game action.
/// </summary>
/// <param name="Accepted">Whether the action is valid in the supplied game state.</param>
/// <param name="OutcomeKey">Stable key identifying the resulting outcome or rejection reason.</param>
/// <param name="Effects">Direct state changes produced by the action.</param>
/// <param name="Wakes">Persistent consequences produced by the action.</param>
/// <param name="NarrativeFacts">Facts available to downstream narrative presentation.</param>
/// <param name="RulesVersion">Version of the game rules used to produce the result.</param>
/// <see href="../../docs/code/src/Fatewake.GameEngine/ActionResolution.md">ActionResolution documentation</see>
public sealed record ActionResolution(
    bool Accepted,
    string OutcomeKey,
    IReadOnlyList<StateEffect> Effects,
    IReadOnlyList<WakeEffect> Wakes,
    IReadOnlyDictionary<string, string> NarrativeFacts,
    string RulesVersion);
