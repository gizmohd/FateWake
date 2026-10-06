namespace Fatewake.Infrastructure.Persistence;
/// <summary>Represents the durable scheduling and execution state of a work step.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/WorkStepStatus.md">WorkStepStatus documentation</see>.</remarks>
public enum WorkStepStatus { Pending=0, Ready=1, Leased=2, Completed=3, Retry=4, Failed=5, Cancelled=6 }