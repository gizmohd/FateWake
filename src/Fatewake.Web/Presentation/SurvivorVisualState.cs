namespace Fatewake.Web.Presentation;

/// <summary>Captures the versioned visual and loadout state of a survivor.</summary>
/// <param name="SurvivorId">Survivor whose visual state is represented.</param>
/// <param name="StateVersion">Version of the canonical visual state.</param>
/// <param name="EffectiveAt">Time at which this state became effective.</param>
/// <param name="PoseKey">Default pose key.</param>
/// <param name="Loadout">Equipped visuals included in the state.</param>
/// <param name="Traits">Normalized traits used by visual resolution.</param>
/// <see href="../../../docs/code/src/Fatewake.Web/Presentation/SurvivorVisualState.md">SurvivorVisualState documentation</see>
public sealed record SurvivorVisualState(
    Guid SurvivorId,
    long StateVersion,
    DateTimeOffset EffectiveAt,
    string PoseKey,
    IReadOnlyList<EquippedVisual> Loadout,
    IReadOnlyDictionary<string, string> Traits);
