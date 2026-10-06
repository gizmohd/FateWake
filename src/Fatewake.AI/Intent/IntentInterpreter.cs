using Fatewake.GameEngine;

namespace Fatewake.AI.Intent;

/// <summary>Translates player input into a normalized action for deterministic game-rule evaluation.</summary>
/// <see href="../../../docs/code/src/Fatewake.AI/Intent/IntentInterpreter.md">IIntentInterpreter documentation</see>
public interface IIntentInterpreter
{
    /// <summary>Interprets player input against the current game context.</summary>
    /// <param name="state">Current game snapshot used to interpret the input.</param>
    /// <param name="input">Player-provided text.</param>
    /// <param name="ct">Token used to cancel interpretation.</param>
    /// <returns>A normalized candidate action; game outcomes remain the responsibility of the GameEngine.</returns>
    Task<CandidateAction> InterpretAsync(GameSnapshot state, string input, CancellationToken ct = default);
}
