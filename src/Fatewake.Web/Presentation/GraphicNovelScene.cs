namespace Fatewake.Web.Presentation;

/// <summary>Represents a scene presented as a sequence of graphic-novel panels.</summary>
/// <param name="Key">Stable scene identifier.</param>
/// <param name="Eyebrow">Short contextual label displayed above the title.</param>
/// <param name="Title">Scene title.</param>
/// <param name="Panels">Ordered panels that make up the scene.</param>
/// <see href="../../../docs/code/src/Fatewake.Web/Presentation/GraphicNovelScene.md">GraphicNovelScene documentation</see>
public sealed record GraphicNovelScene(
    string Key,
    string Eyebrow,
    string Title,
    IReadOnlyList<GraphicNovelPanel> Panels);
