namespace Fatewake.Infrastructure.Work;

/// <summary>Configures durable worker lease, polling, and per-queue concurrency behavior.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Work/WorkExecutionOptions.md">WorkExecutionOptions documentation</see>.</remarks>
public sealed class WorkExecutionOptions
{
    /// <summary>Gets or sets how long a claimed step remains owned without renewal.</summary>
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Gets or sets the PostgreSQL recovery polling interval.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Gets configurable concurrent worker slots per logical queue for each process or pod.</summary>
    public Dictionary<string, int> QueueConcurrency { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["art.generate"] = 1,
        ["art.encode"] = 2,
        ["art.validate"] = 2,
        ["narrative.render"] = 2,
        ["maintenance"] = 1
    };
}