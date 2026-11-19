using System.Text.RegularExpressions;
using Fatewake.GameEngine;

namespace Fatewake.AI.Intent;

/// <summary>Maps common player phrases to supported day-one actions without external AI calls.</summary>
/// <see href="../../../docs/code/src/Fatewake.AI/Intent/BaselineIntentInterpreter.md">BaselineIntentInterpreter documentation</see>
public sealed partial class BaselineIntentInterpreter : IIntentInterpreter
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
            var x when Negation().IsMatch(x) => "unrecognized_intent",
            var x when Radio().IsMatch(x) => "investigate_radio",
            var x when Call().IsMatch(x) => "call_from_safety",
            var x when Help().IsMatch(x) => "help_injured_stranger",
            var x when Stay().IsMatch(x) => "stay_inside",
            _ => "unrecognized_intent"
        };
        return Task.FromResult(new CandidateAction(action, new Dictionary<string, string>(), input));
    }

    [GeneratedRegex(@"\b(?:don't|don’t|won't|won’t|never|not|refuse)\b")]
    private static partial Regex Negation();

    [GeneratedRegex(@"\b(?:radio|broadcast|frequency)\b")]
    private static partial Regex Radio();

    [GeneratedRegex(@"\b(?:call|shout|yell)\b")]
    private static partial Regex Call();

    [GeneratedRegex(@"\b(?:help|assist|support|first aid|run over)\b")]
    private static partial Regex Help();

    [GeneratedRegex(@"\b(?:stay inside|stay indoors|remain inside|remain indoors|keep watching|watch from)\b")]
    private static partial Regex Stay();
}
