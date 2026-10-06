namespace Fatewake.Web.Presentation;

/// <summary>Identifies the presentation purpose of a scene beat.</summary>
/// <see href="../../../docs/code/src/Fatewake.Web/Presentation/BeatKind.md">BeatKind documentation</see>
public enum BeatKind
{
    /// <summary>Establishes the scene, location, or situation.</summary>
    Establishing,
    /// <summary>Presents descriptive narration.</summary>
    Narration,
    /// <summary>Presents dialogue.</summary>
    Dialogue,
    /// <summary>Requests or presents a player interaction.</summary>
    Interaction,
    /// <summary>Shows consequences of a prior action.</summary>
    Consequence,
    /// <summary>Shows an unusual or unexplained event.</summary>
    Anomaly,
    /// <summary>Moves the scene to another beat or state.</summary>
    Transition
}
