namespace Fatewake.Web.Presentation;

/// <summary>Contains the visible visuals resolved for a survivor in one scene context.</summary>
/// <param name="SurvivorId">Survivor represented by the projection.</param>
/// <param name="StateVersion">Canonical visual state version used to produce the projection.</param>
/// <param name="Context">Scene context used during resolution.</param>
/// <param name="Visible">Resolved, ordered visuals that should be rendered.</param>
/// <see href="../../../docs/code/src/Fatewake.Web/Presentation/SurvivorVisualProjection.md">SurvivorVisualProjection documentation</see>
public sealed record SurvivorVisualProjection(
    Guid SurvivorId,
    long StateVersion,
    SceneVisualContext Context,
    IReadOnlyList<VisibleVisual> Visible);
