namespace Fatewake.Infrastructure.Persistence;
/// <summary>Persists a prerequisite edge between two durable work steps.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/WorkStepDependencyRecord.md">WorkStepDependencyRecord documentation</see>.</remarks>
public sealed class WorkStepDependencyRecord
{
    public Guid StepId{get;set;}
    public Guid DependsOnStepId{get;set;}
}