namespace Fatewake.Web.Presentation;

/// <summary>Defines placement and rendering attributes for one asset in a composition.</summary>
/// <param name="AssetKey">Stable key of the artwork asset.</param>
/// <param name="AssetVersion">Version of the artwork asset.</param>
/// <param name="Z">Stacking order; larger values render above smaller values.</param>
/// <param name="X">Normalized horizontal center position.</param>
/// <param name="Y">Normalized vertical center position.</param>
/// <param name="Width">Normalized rendered width.</param>
/// <param name="Height">Normalized rendered height.</param>
/// <param name="Opacity">Opacity from zero to one.</param>
/// <param name="Scale">Additional relative scale.</param>
/// <param name="Rotation">Rotation in degrees.</param>
/// <param name="Anchor">Anchor point used to position the layer.</param>
/// <see href="../../../docs/code/src/Fatewake.Web/Presentation/ArtLayer.md">ArtLayer documentation</see>
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
