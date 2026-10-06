namespace Fatewake.Infrastructure.Work;
/// <summary>Defines one step in a durable dependency-aware job graph.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Work/WorkStepDefinition.md">WorkStepDefinition documentation</see>.</remarks>
public sealed record WorkStepDefinition(string Key,string StepType,string Queue,string Input,int Priority=0,int MaxAttempts=5,IReadOnlyList<string>? DependsOn=null);