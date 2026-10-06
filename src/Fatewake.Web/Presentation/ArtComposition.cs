namespace Fatewake.Web.Presentation;

/// <summary>Defines the dimensions and ordered visual layers of a reusable scene composition.</summary>
/// <param name="Key">Stable composition identifier.</param>
/// <param name="Version">Composition version.</param>
/// <param name="AspectWidth">Width component of the target aspect ratio.</param>
/// <param name="AspectHeight">Height component of the target aspect ratio.</param>
/// <param name="Layers">Assets and placement instructions that form the composition.</param>
/// <see href="../../../docs/code/src/Fatewake.Web/Presentation/ArtComposition.md">ArtComposition documentation</see>
public sealed record ArtComposition(
    string Key,
    int Version,
    double AspectWidth,
    double AspectHeight,
    IReadOnlyList<ArtLayer> Layers);
