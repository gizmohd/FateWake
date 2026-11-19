namespace Fatewake.Web.Presentation;

/// <summary>Contains only audience-safe branch metadata and the expected active event version.</summary>
/// <see href="../../../docs/code/src/Fatewake.Web/Presentation/TimelineBranchPage.md">Documentation</see>
public sealed record TimelineBranchPage(Guid SurvivorId, Guid CurrentTimelineId, bool Eligible, long Version,
    int Page, bool HasMore, TimelineBranchSummary[] Branches, int CurrentSurvivorDay = 1, string? CurrentTimeLabel = null);
