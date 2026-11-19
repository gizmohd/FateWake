namespace Fatewake.GameEngine;

/// <summary>Outcome of validating a player's selection against the decision a scene actually offered.</summary>
/// <param name="Accepted">Whether the selection is valid.</param>
/// <param name="ActionType">Offered action type, or <c>freeform</c> when accepted free text.</param>
/// <param name="FreeformText">Sanitized free text, only for freeform selections.</param>
/// <see href="../../docs/code/src/Fatewake.GameEngine/SceneChoiceResult.md">SceneChoiceResult documentation</see>
public sealed record SceneChoiceResult(bool Accepted, string ActionType, string? FreeformText);
