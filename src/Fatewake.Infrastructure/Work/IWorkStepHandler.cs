namespace Fatewake.Infrastructure.Work;
/// <summary>Executes one durable work-step type after the step has been leased by a worker.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Work/IWorkStepHandler.md">IWorkStepHandler documentation</see>.</remarks>
public interface IWorkStepHandler
{
    /// <summary>Gets the durable step type handled by this implementation.</summary>
    string StepType{get;}
    /// <summary>Executes the idempotent step with durable job and lease context.</summary>
    Task<string?> ExecuteAsync(WorkStepExecutionContext context,CancellationToken ct);
}