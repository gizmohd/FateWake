using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace Fatewake.Observability;

/// <summary>Provides payload-free operation spans and bounded-cardinality latency measurements to runtime libraries.</summary>
/// <see href="../../docs/code/src/Fatewake.Observability/OperationTelemetry.md">Documentation</see>
public sealed class OperationTelemetry : IDisposable
{
    /// <summary>Shared source/meter name registered by every host.</summary>
    public const string SourceName = "Fatewake.Operations";
    private static readonly ActivitySource Source = new(SourceName);
    private static readonly Meter Meter = new(SourceName);
    private static readonly Histogram<double> Duration = Meter.CreateHistogram<double>("fatewake.operation.duration", "s");
    private static readonly Counter<long> Failures = Meter.CreateCounter<long>("fatewake.operation.failures");
    private readonly long started = Stopwatch.GetTimestamp();
    private readonly string operation;
    private readonly ILogger? logger;
    private readonly Activity? activity;
    private string outcome = "ended";
    private bool disposed;

    private OperationTelemetry(string operation, ILogger? logger)
    {
        this.operation = operation;
        this.logger = logger;
        activity = Source.StartActivity(operation);
        logger?.LogDebug("Operation {Operation} started", operation);
    }

    /// <summary>Starts a fixed-name operation without collecting arguments, payloads, credentials or identifiers.</summary>
    /// <param name="operation">Constant operation name; never a user-supplied value.</param>
    /// <param name="logger">Optional typed Microsoft logger from the owning runtime service.</param>
    /// <returns>Scope that records elapsed time on disposal.</returns>
    public static OperationTelemetry Start(string operation, ILogger? logger = null) => new(operation, logger);

    /// <summary>Marks an operation failure without exporting exception messages or secret-bearing payloads.</summary>
    /// <param name="exception">Failure used only to distinguish cancellation and exception type.</param>
    public void Fail(Exception exception)
    {
        outcome = exception is OperationCanceledException ? "cancelled" : "error";
        activity?.SetStatus(outcome == "error" ? ActivityStatusCode.Error : ActivityStatusCode.Unset);
        activity?.SetTag("error.type", exception.GetType().FullName);
    }

    /// <summary>Ends the span and records low-cardinality duration and failures once.</summary>
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        var elapsed = Stopwatch.GetElapsedTime(started).TotalSeconds;
        var tags = new TagList { { "operation", operation }, { "outcome", outcome } };
        Duration.Record(elapsed, tags);
        if (outcome == "error") Failures.Add(1, tags);
        activity?.SetTag("operation.outcome", outcome);
        logger?.LogDebug("Operation {Operation} ended with {Outcome} in {ElapsedSeconds} seconds", operation, outcome, elapsed);
        activity?.Dispose();
    }
}
