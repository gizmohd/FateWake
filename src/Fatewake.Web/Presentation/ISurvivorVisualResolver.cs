namespace Fatewake.Web.Presentation;

/// <summary>Projects canonical survivor visuals into scene-ready assets.</summary>
/// <see href="../../../docs/code/src/Fatewake.Web/Presentation/ISurvivorVisualResolver.md">ISurvivorVisualResolver documentation</see>
public interface ISurvivorVisualResolver
{
    /// <summary>Resolves which equipped visuals are visible in the supplied scene context.</summary>
    /// <param name="state">Canonical survivor visual state.</param>
    /// <param name="context">Scene pose, camera, and item visibility overrides.</param>
    /// <returns>An ordered projection of visuals to render.</returns>
    SurvivorVisualProjection Project(SurvivorVisualState state, SceneVisualContext context);

    /// <summary>Creates a fingerprinted snapshot of a resolved projection.</summary>
    /// <param name="identity">Reusable base identity asset for the survivor.</param>
    /// <param name="projection">Resolved visual projection to capture.</param>
    /// <returns>An immutable snapshot and its deterministic fingerprint.</returns>
    SurvivorVisualSnapshot Snapshot(SurvivorVisualIdentity identity, SurvivorVisualProjection projection);

    /// <summary>Converts the snapshot's visible visuals into composition layers.</summary>
    /// <param name="snapshot">Snapshot containing the resolved visuals.</param>
    /// <param name="baseZ">Starting stacking order for generated layers.</param>
    /// <param name="x">Normalized horizontal center position.</param>
    /// <param name="y">Normalized vertical center position.</param>
    /// <param name="width">Normalized layer width.</param>
    /// <param name="height">Normalized layer height.</param>
    /// <returns>Ordered artwork layers for scene composition.</returns>
    IReadOnlyList<ArtLayer> Compose(SurvivorVisualSnapshot snapshot, int baseZ, double x, double y, double width, double height);
}
