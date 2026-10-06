namespace Fatewake.Web.Presentation;

/// <summary>Describes an equipped asset and the rules for showing it in a scene.</summary>
/// <param name="Slot">Visual layer occupied by the asset.</param>
/// <param name="AssetKey">Stable artwork asset key.</param>
/// <param name="AssetVersion">Version of the artwork asset.</param>
/// <param name="ItemInstanceId">Optional identity of the specific equipped item.</param>
/// <param name="Priority">Order within the visual slot.</param>
/// <param name="CarryVisibility">Default visibility of a carried item.</param>
/// <param name="CompatiblePoses">Optional list of poses where the asset is visible.</param>
/// <see href="../../../docs/code/src/Fatewake.Web/Presentation/EquippedVisual.md">EquippedVisual documentation</see>
public sealed record EquippedVisual(
    VisualSlot Slot,
    string AssetKey,
    int AssetVersion,
    string? ItemInstanceId = null,
    int Priority = 0,
    CarryVisibility CarryVisibility = CarryVisibility.NormallyVisible,
    IReadOnlyList<string>? CompatiblePoses = null);
