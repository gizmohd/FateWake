namespace Fatewake.Infrastructure.Work;
/// <summary>Creates idempotent durable jobs from validated dependency graphs.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Work/IWorkJobBuilder.md">IWorkJobBuilder documentation</see>.</remarks>
public interface IWorkJobBuilder
{
    /// <summary>Creates or returns an existing durable job for the supplied idempotency key.</summary>
    Task<CreatedWorkJob> CreateAsync(WorkJobDefinition definition,CancellationToken ct=default);
}