namespace Fatewake.Infrastructure.Story;

/// <summary>Structured scene authored by the narrator and persisted verbatim after validation.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Story/SceneSpec.md">Documentation</see>.</remarks>
/// <param name="Title">Player-facing scene title.</param>
/// <param name="Summary">Short factual summary used for later retrieval and continuity.</param>
/// <param name="TimeLabel">In-world time label.</param>
/// <param name="Beats">Ordered beats.</param>
/// <param name="Choices">Zero choices, or two to four when the scene is a decision point.</param>
public sealed record SceneSpec(string Title, string Summary, string TimeLabel,
    IReadOnlyList<SceneBeatSpec> Beats, IReadOnlyList<SceneChoiceSpec> Choices)
{
    /// <summary>Gets whether the scene offers a decision.</summary>
    public bool HasDecision => Choices.Count > 0;
}
