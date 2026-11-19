namespace Fatewake.Web.Presentation;

/// <summary>Paginates reached restore points on the selected branch.</summary>
/// <see href="../../../docs/code/src/Fatewake.Web/Presentation/TimelineCheckpointPage.md">Documentation</see>
public sealed record TimelineCheckpointPage(int Page, bool HasMore, TimelineCheckpointSummary[] Checkpoints);
