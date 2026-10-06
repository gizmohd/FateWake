namespace Fatewake.Infrastructure.Persistence;
/// <summary>Returns the fenced lease token and execution data for an atomically claimed work step.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/WorkLease.md">WorkLease documentation</see>.</remarks>
public sealed record WorkLease(Guid StepId,Guid JobId,string StepType,string Queue,string Input,Guid LeaseToken,int Attempt);