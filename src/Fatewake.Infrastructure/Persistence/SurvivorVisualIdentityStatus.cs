namespace Fatewake.Infrastructure.Persistence;
/// <summary>Describes the lifecycle state of a survivor's active visual identity.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/SurvivorVisualIdentityStatus.md">SurvivorVisualIdentityStatus documentation</see>.</remarks>
public enum SurvivorVisualIdentityStatus
{
    Default=0,
    GenerationPending=1,
    Generated=2,
    Reused=3,
    GenerationFailed=4
}