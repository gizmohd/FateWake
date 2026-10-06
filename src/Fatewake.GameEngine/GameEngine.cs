namespace Fatewake.GameEngine;

/// <summary>
/// Defines the deterministic game rules boundary.
/// </summary>
/// <see href="../../docs/code/src/Fatewake.GameEngine/GameEngine.md">IGameEngine documentation</see>
public interface IGameEngine
{
    /// <summary>
    /// Resolves an action against the supplied canonical game snapshot.
    /// </summary>
    /// <param name="state">The authoritative state used to evaluate the action.</param>
    /// <param name="action">The candidate action submitted for resolution.</param>
    /// <returns>The accepted or rejected action result, including effects and narrative facts.</returns>
    ActionResolution Resolve(GameSnapshot state, CandidateAction action);
}
