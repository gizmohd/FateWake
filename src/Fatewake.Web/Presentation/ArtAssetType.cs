namespace Fatewake.Web.Presentation;

/// <summary>Identifies the visual role of a reusable artwork asset.</summary>
/// <see href="../../../docs/code/src/Fatewake.Web/Presentation/ArtAssetType.md">ArtAssetType documentation</see>
public enum ArtAssetType
{
    /// <summary>Scene or environment background.</summary>
    Background,
    /// <summary>Character body pose artwork.</summary>
    CharacterPose,
    /// <summary>Character expression artwork.</summary>
    CharacterExpression,
    /// <summary>Standalone scene prop.</summary>
    Prop,
    /// <summary>Foreground overlay artwork.</summary>
    Foreground,
    /// <summary>Atmospheric scene layer.</summary>
    Atmosphere,
    /// <summary>Scene lighting layer.</summary>
    Lighting,
    /// <summary>Visual effect layer.</summary>
    Effect,
    /// <summary>Standalone hero illustration.</summary>
    HeroIllustration
}
