namespace Fatewake.Web.Presentation;

/// <summary>Defines the content and behavior of one beat in a runtime scene.</summary>
/// <param name="Key">Stable beat identifier within the scene.</param>
/// <param name="Kind">Presentation purpose of the beat.</param>
/// <param name="ArtworkKey">Artwork composition key for the beat.</param>
/// <param name="Time">Optional in-world time label.</param>
/// <param name="Status">Optional status label.</param>
/// <param name="Narration">Optional narration text.</param>
/// <param name="Dialogue">Optional dialogue line.</param>
/// <param name="Interactions">Actions offered during the beat.</param>
/// <param name="AutoAdvance">Whether the runtime advances without player input.</param>
/// <param name="AutoAdvanceMilliseconds">Delay before automatic advancement.</param>
/// <see href="../../../docs/code/src/Fatewake.Web/Presentation/BeatDefinition.md">BeatDefinition documentation</see>
public sealed record BeatDefinition(
    string Key,
    BeatKind Kind,
    string ArtworkKey,
    string? Time,
    string? Status,
    string? Narration,
    DialogueLine? Dialogue,
    IReadOnlyList<SceneInteraction> Interactions,
    bool AutoAdvance = false,
    int AutoAdvanceMilliseconds = 0);
