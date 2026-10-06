namespace Fatewake.Web.Presentation;

public enum VisualSlot { Head, Face, TorsoBase, TorsoOuter, Legs, Feet, Hands, Back, PrimaryCarry, SecondaryCarry, Belt, Accessory, Condition, Injury, StoryMark }

public sealed record SurvivorVisualIdentity(
    Guid SurvivorId,
    string IdentityAssetKey,
    int IdentityAssetVersion);

public sealed record EquippedVisual(
    VisualSlot Slot,
    string AssetKey,
    int AssetVersion,
    string? ItemInstanceId = null,
    int Priority = 0);

public sealed record SurvivorVisualState(
    Guid SurvivorId,
    long StateVersion,
    DateTimeOffset EffectiveAt,
    string PoseKey,
    IReadOnlyList<EquippedVisual> Equipped,
    IReadOnlyDictionary<string,string> Traits);

public sealed record SurvivorVisualSnapshot(
    Guid SurvivorId,
    long StateVersion,
    string Fingerprint,
    DateTimeOffset CapturedAt,
    SurvivorVisualState State);
