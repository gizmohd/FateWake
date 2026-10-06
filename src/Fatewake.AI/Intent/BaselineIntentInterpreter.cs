using Fatewake.GameEngine;

namespace Fatewake.AI.Intent;

/// <summary>Maps common player phrases to supported day-one actions without external AI calls.</summary>
/// <see href="../../../docs/code/src/Fatewake.AI/Intent/BaselineIntentInterpreter.md">BaselineIntentInterpreter documentation</see>
public sealed class BaselineIntentInterpreter : IIntentInterpreter
{
    /// <summary>Maps the input phrase to a normalized action while preserving the original text.</summary>
    /// <param name="state">Current game snapshot; the baseline mapping does not inspect state.</param>
    /// <param name="input">Player-provided text.</param>
    /// <param name="ct">Token used to cancel interpretation.</param>
    /// <returns>The normalized candidate action, including an unrecognized action when no phrase matches.</returns>
    public Task<CandidateAction> InterpretAsync(GameSnapshot state, string input, CancellationToken ct = default)
    {
        var text = input.Trim().ToLowerInvariant();
        var action = text switch
        {
            var x when x.Contains("help") || x.Contains("first aid") || x.Contains("run over") => "help_injured_stranger",
            var x when x.Contains("radio") || x.Contains("broadcast") => "investigate_radio",
            var x when x.Contains("door") || x.Contains("call") || x.Contains("yell") => "call_from_safety",
            var x when x.Contains("stay") || x.Contains("watch") || x.Contains("inside") => "stay_inside",
            _ => "unrecognized_intent"
        };
        return Task.FromResult(new CandidateAction(action, new Dictionary<string, string>(), input));
    }
}
