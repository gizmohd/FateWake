namespace Fatewake.GameEngine;

/// <summary>
/// Enforces the pacing rule that no more than four consecutive scenes may omit a player decision.
/// </summary>
/// <see href="../../docs/code/src/Fatewake.GameEngine/SceneDecisionPolicy.md">SceneDecisionPolicy documentation</see>
public static class SceneDecisionPolicy
{
    /// <summary>Largest number of consecutive scenes allowed without a decision.</summary>
    public const int MaxDecisionFreeScenes = 4;

    /// <summary>Whether the next scene must offer a decision given the current decision-free streak.</summary>
    /// <param name="decisionFreeStreak">Consecutive decision-free scenes immediately before the next scene.</param>
    /// <returns>True when the streak has reached the maximum.</returns>
    public static bool RequiresDecision(int decisionFreeStreak) => decisionFreeStreak >= MaxDecisionFreeScenes;

    /// <summary>Validates a candidate scene and computes the resulting streak.</summary>
    /// <param name="previousStreak">Decision-free streak before the candidate scene.</param>
    /// <param name="hasDecision">Whether the candidate scene offers at least one decision.</param>
    /// <param name="streak">Streak after the scene: zero when it has a decision, otherwise one more than before.</param>
    /// <returns>False when the scene omits a decision although one is required.</returns>
    public static bool TryAccept(int previousStreak, bool hasDecision, out int streak)
    {
        streak = hasDecision ? 0 : Math.Max(0, previousStreak) + 1;
        return hasDecision || !RequiresDecision(previousStreak);
    }
}
