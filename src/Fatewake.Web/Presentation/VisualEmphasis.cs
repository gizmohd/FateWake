namespace Fatewake.Web.Presentation;

/// <summary>Overrides the default visibility or presentation of an equipped visual.</summary>
/// <see href="../../../docs/code/src/Fatewake.Web/Presentation/VisualEmphasis.md">VisualEmphasis documentation</see>
public enum VisualEmphasis
{
    /// <summary>Use the visual's normal visibility rules.</summary>
    Normal,
    /// <summary>Hide the visual in this scene.</summary>
    Suppress,
    /// <summary>Show the visual even when normally concealed.</summary>
    Reveal,
    /// <summary>Show the visual as currently held or used.</summary>
    InHand
}
