using Fatewake.Infrastructure.Authentication;

namespace Fatewake.Infrastructure.Story;

/// <summary>Server-assembled, bounded context handed to the narrator.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Story/SceneAuthoringContext.md">Documentation</see>.</remarks>
/// <param name="SurvivorName">Display name of the survivor protagonist.</param>
/// <param name="SurvivorDay">Current in-game day.</param>
/// <param name="Sequence">One-based scene number on the timeline.</param>
/// <param name="RequiresDecision">True when the pacing rule forces a decision in this scene.</param>
/// <param name="CanonPassages">Retrieved canon and prior story passages (untrusted reference text).</param>
/// <param name="RecentSummaries">Summaries of the most recent scenes, oldest first.</param>
/// <param name="OwnerAccountId">Account for usage accounting.</param>
/// <param name="SurvivorId">Survivor for usage accounting.</param>
/// <param name="PlayerChoice">The player's decision in the previous scene, as untrusted intent text.</param>
/// <param name="ContentLevel">Effective age-capped level for the narrator and reviewer.</param>
/// <param name="BranchFocus">Optional generic focus from a compatible branch.</param>
public sealed record SceneAuthoringContext(string SurvivorName, int SurvivorDay, int Sequence, bool RequiresDecision,
    IReadOnlyList<string> CanonPassages, IReadOnlyList<string> RecentSummaries, Guid? OwnerAccountId, Guid SurvivorId,
    string? PlayerChoice = null, ContentLevel ContentLevel = ContentLevel.Under14, string? BranchFocus = null);
