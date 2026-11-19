namespace Fatewake.Web.Presentation;

/// <summary>Identifies a reached checkpoint with an authored story-time label.</summary>
/// <see href="../../../docs/code/src/Fatewake.Web/Presentation/TimelineCheckpointSummary.md">Documentation</see>
public sealed record TimelineCheckpointSummary(Guid Id, string Label, string TimeLabel, DateTimeOffset CreatedAt);
