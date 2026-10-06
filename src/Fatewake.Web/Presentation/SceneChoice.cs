namespace Fatewake.Web.Presentation;

/// <summary>Represents a selectable action and its player-facing label.</summary>
/// <param name="ActionType">Stable action identifier submitted when selected.</param>
/// <param name="Label">Text displayed to the player.</param>
/// <see href="../../../docs/code/src/Fatewake.Web/Presentation/SceneChoice.md">SceneChoice documentation</see>
public sealed record SceneChoice(string ActionType, string Label);
