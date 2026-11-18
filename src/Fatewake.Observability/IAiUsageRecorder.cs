namespace Fatewake.Observability;
/// <summary>Durable accounting boundary for every external AI operation.</summary>
public interface IAiUsageRecorder
{
    /// <summary>Records an attempt before an external AI request is submitted.</summary>
    Task<Guid> BeginAsync(AiCallContext context, CancellationToken ct = default);
    /// <summary>Persists reported identity and measurements before processing provider output.</summary>
    Task ReportAsync(Guid attemptId, AiUsageReport report, CancellationToken ct = default);
    /// <summary>Closes an attempt without discarding any reported consumption.</summary>
    Task FinishAsync(Guid attemptId, string outcome, CancellationToken ct = default);
}
/// <summary>Safe operation identity; contains no prompt or credential data.</summary>
public sealed record AiCallContext(string Provider, string Model, string Operation, string AttemptKey,
    Guid? WorkJobId = null, Guid? AccountId = null, Guid? SurvivorId = null);
/// <summary>Provider-reported measurements; missing values remain unknown.</summary>
public sealed record AiUsageReport(string? ProviderRequestId = null, long? InputTokens = null, long? OutputTokens = null,
    long? TotalTokens = null, long? CachedTokens = null, long? TextInputTokens = null, long? ImageInputTokens = null,
    long? ImageOutputTokens = null, decimal? Credits = null, decimal? ComputeSeconds = null, decimal? Cost = null, string? Currency = null);
