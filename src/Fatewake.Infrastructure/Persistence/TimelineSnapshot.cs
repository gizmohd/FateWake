namespace Fatewake.Infrastructure.Persistence;

/// <summary>Contains implemented branch state, frozen journal disclosure and causal references, never reconstructed prose as rules.</summary>
/// <see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/TimelineSnapshot.md">Documentation</see>
public sealed record TimelineSnapshot(
    int SchemaVersion,
    string RulesVersion,
    string ContentVersion,
    int SurvivorDay,
    SurvivorStatus Status,
    string Facts,
    EventInstanceRecord Episode,
    IReadOnlyList<WakeRecord> Wakes,
    IReadOnlyList<StoryPanelRecord> Panels,
    SurvivorVisualIdentityRecord? VisualIdentity);
