using Fatewake.GameEngine;

namespace Fatewake.AI.Intent;

public interface IIntentInterpreter
{
    Task<CandidateAction> InterpretAsync(GameSnapshot state, string input, CancellationToken ct = default);
}

// Deterministic baseline. An AI-backed implementation can replace this without changing GameEngine authority.
public sealed class BaselineIntentInterpreter : IIntentInterpreter
{
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
        return Task.FromResult(new CandidateAction(action, new Dictionary<string,string>(), input));
    }
}
