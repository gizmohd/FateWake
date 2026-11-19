namespace Fatewake.Infrastructure.Story;

/// <summary>What the client needs to render the survivor's current scene or its loading/failure state.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Story/StorySceneView.md">Documentation</see>.</remarks>
/// <param name="Status">Pending, Ready or Failed.</param>
/// <param name="Sequence">One-based scene number.</param>
/// <param name="SurvivorDay">In-game day.</param>
/// <param name="Error">Admin-readable failure explanation, only when Failed.</param>
/// <param name="Scene">The validated scene, only when Ready.</param>
public sealed record StorySceneView(string Status, int Sequence, int SurvivorDay, string? Error, SceneSpec? Scene);
