namespace Fatewake.Web.Presentation;

public sealed record GraphicNovelScene(
    string Key,
    string Eyebrow,
    string Title,
    IReadOnlyList<GraphicNovelPanel> Panels);

public sealed record GraphicNovelPanel(
    string Key,
    string? ArtworkKey,
    string? Time,
    string? Status,
    string? Narration,
    DialogueLine? Dialogue,
    IReadOnlyList<SceneChoice> Choices,
    bool AllowFreeform = false);

public sealed record DialogueLine(string Speaker, string Text);
public sealed record SceneChoice(string ActionType, string Label);
