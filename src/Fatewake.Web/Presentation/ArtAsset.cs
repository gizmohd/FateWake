namespace Fatewake.Web.Presentation;

/// <summary>Describes a versioned artwork resource available to scene composition.</summary>
/// <param name="Key">Stable artwork identifier.</param>
/// <param name="Version">Version of the asset.</param>
/// <param name="Type">Visual role of the asset.</param>
/// <param name="Source">Portable source key or URI used to resolve the artwork.</param>
/// <param name="CharacterKey">Optional character associated with the artwork.</param>
/// <param name="LocationKey">Optional location associated with the artwork.</param>
/// <param name="PromptKey">Optional key for the prompt that reproduces the artwork.</param>
/// <param name="FocalX">Horizontal focal point, normalized from zero to one.</param>
/// <param name="FocalY">Vertical focal point, normalized from zero to one.</param>
/// <see href="../../../docs/code/src/Fatewake.Web/Presentation/ArtAsset.md">ArtAsset documentation</see>
public sealed record ArtAsset(
    string Key,
    int Version,
    ArtAssetType Type,
    string Source,
    string? CharacterKey = null,
    string? LocationKey = null,
    string? PromptKey = null,
    double FocalX = .5,
    double FocalY = .5);
