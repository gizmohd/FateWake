namespace Fatewake.Web.Presentation;

/// <summary>Provides scene-specific context for resolving a survivor's visible visuals.</summary>
/// <param name="SceneKey">Stable scene identifier.</param>
/// <param name="PoseKey">Pose used in the scene.</param>
/// <param name="CameraKey">Camera framing identifier.</param>
/// <param name="ItemEmphasis">Per-item visibility overrides keyed by item instance id.</param>
/// <see href="../../../docs/code/src/Fatewake.Web/Presentation/SceneVisualContext.md">SceneVisualContext documentation</see>
public sealed record SceneVisualContext(
    string SceneKey,
    string PoseKey,
    string CameraKey,
    IReadOnlyDictionary<string, VisualEmphasis> ItemEmphasis);
