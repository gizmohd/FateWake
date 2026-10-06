namespace Fatewake.Web.Presentation;

/// <summary>Contains the display and interaction content for one graphic-novel panel.</summary>
/// <param name="Key">Stable panel identifier.</param>
/// <param name="ArtworkKey">Optional artwork key for the panel illustration.</param>
/// <param name="Time">Optional in-world time label.</param>
/// <param name="Status">Optional status label.</param>
/// <param name="Narration">Optional narration text.</param>
/// <param name="Dialogue">Optional dialogue line.</param>
/// <param name="Choices">Choices available in this panel.</param>
/// <param name="AllowFreeform">Whether the panel accepts a free-form action.</param>
/// <see href="../../../docs/code/src/Fatewake.Web/Presentation/GraphicNovelPanel.md">GraphicNovelPanel documentation</see>
public sealed record GraphicNovelPanel(
    string Key,
    string? ArtworkKey,
    string? Time,
    string? Status,
    string? Narration,
    DialogueLine? Dialogue,
    IReadOnlyList<SceneChoice> Choices,
    bool AllowFreeform = false);
