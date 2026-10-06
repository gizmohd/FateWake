namespace Fatewake.GameEngine;

/// <summary>
/// Describes a persistent consequence that may influence later events or relationships.
/// </summary>
/// <param name="Type">Stable category of the Wake effect.</param>
/// <param name="Scope">Domain within which the effect applies.</param>
/// <param name="Target">Optional target of the effect.</param>
/// <param name="Severity">Relative strength of the effect.</param>
/// <param name="Properties">Additional typed-by-convention effect data.</param>
/// <see href="../../docs/code/src/Fatewake.GameEngine/WakeEffect.md">WakeEffect documentation</see>
public sealed record WakeEffect(
    string Type,
    string Scope,
    string? Target,
    int Severity,
    IReadOnlyDictionary<string, string> Properties);
