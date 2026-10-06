namespace Fatewake.GameEngine;

/// <summary>
/// Describes a direct change to a key in canonical game state.
/// </summary>
/// <param name="Type">Category of the state value being changed.</param>
/// <param name="Key">Stable key identifying the state value.</param>
/// <param name="Value">New value to apply.</param>
/// <see href="../../docs/code/src/Fatewake.GameEngine/StateEffect.md">StateEffect documentation</see>
public sealed record StateEffect(string Type, string Key, string Value);
