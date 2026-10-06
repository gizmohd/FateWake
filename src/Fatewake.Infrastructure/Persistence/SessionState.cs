namespace Fatewake.Infrastructure.Persistence;

/// <summary>Canonical identifiers and presentation state returned to a game client.</summary>
/// <param name="SurvivorId">Survivor identifier.</param>
/// <param name="TimelineId">Survivor timeline identifier.</param>
/// <param name="EventInstanceId">Active event identifier.</param>
/// <param name="SurvivorDay">Current survivor day.</param>
/// <param name="EventKey">Active event definition.</param>
/// <param name="Status">Event status.</param>
/// <param name="LastOutcome">Previously persisted action outcome.</param>
/// <param name="NarrativeFacts">Authoritative narrative facts.</param>
/// <param name="SceneKey">Current presentation scene.</param>
/// <param name="BeatKey">Current presentation beat.</param>
/// <see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/SessionState.md">SessionState documentation</see>
public sealed record SessionState(Guid SurvivorId, Guid TimelineId, Guid EventInstanceId, int SurvivorDay,
    string EventKey, string Status, string? LastOutcome, IReadOnlyDictionary<string, string> NarrativeFacts, string SceneKey, string BeatKey);
