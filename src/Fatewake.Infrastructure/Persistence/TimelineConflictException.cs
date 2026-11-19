namespace Fatewake.Infrastructure.Persistence;

/// <summary>Reports a safe, actionable conflict without exposing private state from another timeline.</summary>
/// <see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/TimelineConflictException.md">Documentation</see>
public sealed class TimelineConflictException(string message) : InvalidOperationException(message);
