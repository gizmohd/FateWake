namespace Fatewake.Api;

/// <summary>Identifies the selected source branch and explicit target without accepting client-supplied snapshot state.</summary>
/// <see href="../../docs/code/src/Fatewake.Api/TimelineMutationRequest.md">Documentation</see>
public sealed record TimelineMutationRequest(Guid ExpectedTimelineId, Guid RequestId, Guid? CheckpointId = null,
    Guid? TargetTimelineId = null, long? ExpectedVersion = null);
