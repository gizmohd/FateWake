namespace Fatewake.Infrastructure.Story;

/// <summary>A decision offered at the end of a scene; the engine, not the model, resolves its outcome.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Story/SceneChoiceSpec.md">Documentation</see>.</remarks>
/// <param name="ActionType">Stable snake_case action identifier.</param>
/// <param name="Label">Player-facing label.</param>
/// <param name="AllowFreeform">Whether the player may elaborate in free text.</param>
public sealed record SceneChoiceSpec(string ActionType, string Label, bool AllowFreeform);
