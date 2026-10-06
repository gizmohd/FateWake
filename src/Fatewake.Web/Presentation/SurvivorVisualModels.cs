namespace Fatewake.Web.Presentation;

public enum VisualSlot { Head, Face, TorsoBase, TorsoOuter, Legs, Feet, Hands, Back, PrimaryCarry, SecondaryCarry, Belt, Accessory, Condition, Injury, StoryMark }
public enum CarryVisibility { AlwaysVisible, NormallyVisible, Concealed, Stored }
public enum VisualEmphasis { Normal, Suppress, Reveal, InHand }

public sealed record SurvivorVisualIdentity(Guid SurvivorId, string IdentityAssetKey, int IdentityAssetVersion);

public sealed record EquippedVisual(
    VisualSlot Slot,
    string AssetKey,
    int AssetVersion,
    string? ItemInstanceId = null,
    int Priority = 0,
    CarryVisibility CarryVisibility = CarryVisibility.NormallyVisible,
    IReadOnlyList<string>? CompatiblePoses = null);

public sealed record SurvivorVisualState(
    Guid SurvivorId,
    long StateVersion,
    DateTimeOffset EffectiveAt,
    string PoseKey,
    IReadOnlyList<EquippedVisual> Loadout,
    IReadOnlyDictionary<string,string> Traits);

public sealed record SceneVisualContext(
    string SceneKey,
    string PoseKey,
    string CameraKey,
    IReadOnlyDictionary<string,VisualEmphasis> ItemEmphasis);

public sealed record VisibleVisual(
    EquippedVisual Source,
    VisualEmphasis Emphasis);

public sealed record SurvivorVisualProjection(
    Guid SurvivorId,
    long StateVersion,
    SceneVisualContext Context,
    IReadOnlyList<VisibleVisual> Visible);

public sealed record SurvivorVisualSnapshot(
    Guid SurvivorId,
    long StateVersion,
    string Fingerprint,
    DateTimeOffset CapturedAt,
    SurvivorVisualProjection Projection);
