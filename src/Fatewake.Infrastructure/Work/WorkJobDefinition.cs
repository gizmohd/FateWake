namespace Fatewake.Infrastructure.Work;
/// <summary>Defines a durable job and all steps required to execute it.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Work/WorkJobDefinition.md">WorkJobDefinition documentation</see>.</remarks>
public sealed record WorkJobDefinition(string JobType,string IdempotencyKey,string Payload,IReadOnlyList<WorkStepDefinition> Steps,int Priority=0);