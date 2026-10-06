namespace Fatewake.Infrastructure.Work;
/// <summary>Describes the durable job produced or reused by job creation.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Work/CreatedWorkJob.md">CreatedWorkJob documentation</see>.</remarks>
public sealed record CreatedWorkJob(Guid JobId,bool Existing,IReadOnlyDictionary<string,Guid> StepIds);