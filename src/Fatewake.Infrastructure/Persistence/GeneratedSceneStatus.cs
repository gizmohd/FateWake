namespace Fatewake.Infrastructure.Persistence;

/// <summary>Lifecycle values stored in <see cref="GeneratedSceneRecord.Status"/>.</summary>
/// <see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/GeneratedSceneStatus.md">Documentation</see>
public static class GeneratedSceneStatus
{
    /// <summary>Reserved and queued; no validated content yet.</summary>
    public const string Pending = "Pending";

    /// <summary>Validated and playable; content is immutable from here on.</summary>
    public const string Ready = "Ready";

    /// <summary>Generation exhausted its attempts; <see cref="GeneratedSceneRecord.Error"/> explains why.</summary>
    public const string Failed = "Failed";
}
