namespace Fatewake.Infrastructure.Work;
/// <summary>Executes one durable work-step type after the step has been leased by a worker.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Work/IWorkStepHandler.md">IWorkStepHandler documentation</see>.</remarks>
public interface IWorkStepHandler
{
    /// <summary>Gets the durable step type handled by this implementation.</summary>
    string StepType{get;}
    /// <summary>Executes the idempotent step.</summary><param name="input">Persisted JSON/input payload.</param><param name="ct">Cancellation token.</param><returns>Optional persisted output JSON.</returns>
    Task<string?> ExecuteAsync(string input,CancellationToken ct);
}