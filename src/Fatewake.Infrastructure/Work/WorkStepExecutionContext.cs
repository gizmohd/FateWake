namespace Fatewake.Infrastructure.Work;
/// <summary>Supplies durable job/step identity and persisted input to a work-step handler.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Work/WorkStepExecutionContext.md">WorkStepExecutionContext documentation</see>.</remarks>
public sealed record WorkStepExecutionContext(Guid JobId,Guid StepId,string Input,int Attempt);