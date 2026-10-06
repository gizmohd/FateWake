namespace Fatewake.GameEngine;

/// <summary>
/// Captures the canonical context required to deterministically resolve a game action.
/// </summary>
/// <param name="SurvivorId">Identifier of the survivor taking the action.</param>
/// <param name="TimelineId">Identifier of the timeline in which the action occurs.</param>
/// <param name="SurvivorDay">The survivor's current in-game day.</param>
/// <param name="EventKey">Stable key for the active event.</param>
/// <param name="Facts">Known facts supplied to the game rules.</param>
/// <see href="../../docs/code/src/Fatewake.GameEngine/GameSnapshot.md">GameSnapshot documentation</see>
public sealed record GameSnapshot(
    Guid SurvivorId,
    Guid TimelineId,
    int SurvivorDay,
    string EventKey,
    IReadOnlyDictionary<string, string> Facts);
