namespace Fatewake.Web.Presentation;

/// <summary>Labels preserved branch lineage without exposing checkpoint payloads or hidden causes.</summary>
/// <see href="../../../docs/code/src/Fatewake.Web/Presentation/TimelineBranchSummary.md">Documentation</see>
public sealed record TimelineBranchSummary(Guid TimelineId, Guid? ParentTimelineId, Guid? CheckpointId, string Origin,
    DateTimeOffset CreatedAt, string? CheckpointLabel = null, int? SurvivorDay = null, string? TimeLabel = null);
