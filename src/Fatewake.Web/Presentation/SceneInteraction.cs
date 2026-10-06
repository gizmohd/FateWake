namespace Fatewake.Web.Presentation;

/// <summary>Defines an action offered to the player during a scene beat.</summary>
/// <param name="ActionType">Stable action identifier.</param>
/// <param name="Label">Player-facing action label.</param>
/// <param name="AllowFreeform">Whether the player may provide a free-form action.</param>
/// <see href="../../../docs/code/src/Fatewake.Web/Presentation/SceneInteraction.md">SceneInteraction documentation</see>
public sealed record SceneInteraction(string ActionType, string Label, bool AllowFreeform = false);
