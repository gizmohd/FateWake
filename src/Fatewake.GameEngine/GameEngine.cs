namespace Fatewake.GameEngine;

public interface IGameEngine
{
    ActionResolution Resolve(GameSnapshot state, CandidateAction action);
}

public sealed record GameSnapshot(
    Guid SurvivorId,
    Guid TimelineId,
    int SurvivorDay,
    string EventKey,
    IReadOnlyDictionary<string, string> Facts);

public sealed record CandidateAction(
    string ActionType,
    IReadOnlyDictionary<string, string> Arguments,
    string? RawInput = null);

public sealed record WakeEffect(
    string Type,
    string Scope,
    string? Target,
    int Severity,
    IReadOnlyDictionary<string, string> Properties);

public sealed record StateEffect(string Type, string Key, string Value);

public sealed record ActionResolution(
    bool Accepted,
    string OutcomeKey,
    IReadOnlyList<StateEffect> Effects,
    IReadOnlyList<WakeEffect> Wakes,
    IReadOnlyDictionary<string, string> NarrativeFacts,
    string RulesVersion);
