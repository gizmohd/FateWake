namespace Fatewake.Infrastructure.Persistence;
/// <summary>Represents the durable lifecycle state of a distributed job.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/WorkJobStatus.md">WorkJobStatus documentation</see>.</remarks>
public enum WorkJobStatus { Pending=0, Running=1, Completed=2, Failed=3, Cancelled=4 }