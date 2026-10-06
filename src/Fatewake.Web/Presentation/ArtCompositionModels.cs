namespace Fatewake.Web.Presentation;

public enum ArtAssetType { Background, CharacterPose, CharacterExpression, Prop, Foreground, Atmosphere, Lighting, Effect, HeroIllustration }

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

public sealed record ArtLayer(
    string AssetKey,
    int AssetVersion,
    int Z,
    double X = .5,
    double Y = .5,
    double Width = 1,
    double Height = 1,
    double Opacity = 1,
    double Scale = 1,
    double Rotation = 0,
    string Anchor = "center");

public sealed record ArtComposition(
    string Key,
    int Version,
    double AspectWidth,
    double AspectHeight,
    IReadOnlyList<ArtLayer> Layers);
