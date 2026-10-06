namespace Fatewake.Web.Presentation;

/// <summary>Identifies a character visual layer or equipment position.</summary>
/// <see href="../../../docs/code/src/Fatewake.Web/Presentation/VisualSlot.md">VisualSlot documentation</see>
public enum VisualSlot
{
    /// <summary>Headwear and head identity layer.</summary>
    Head,
    /// <summary>Facial identity and expression layer.</summary>
    Face,
    /// <summary>Base torso clothing layer.</summary>
    TorsoBase,
    /// <summary>Outer torso clothing layer.</summary>
    TorsoOuter,
    /// <summary>Leg clothing layer.</summary>
    Legs,
    /// <summary>Footwear layer.</summary>
    Feet,
    /// <summary>Hand or glove layer.</summary>
    Hands,
    /// <summary>Items carried on the character's back.</summary>
    Back,
    /// <summary>Primary carried item.</summary>
    PrimaryCarry,
    /// <summary>Secondary carried item.</summary>
    SecondaryCarry,
    /// <summary>Belt-mounted items.</summary>
    Belt,
    /// <summary>General accessory layer.</summary>
    Accessory,
    /// <summary>Temporary condition visual layer.</summary>
    Condition,
    /// <summary>Injury visual layer.</summary>
    Injury,
    /// <summary>Persistent story-related visual mark.</summary>
    StoryMark
}
