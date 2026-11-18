namespace Fatewake.Infrastructure.Persistence;
/// <summary>One external AI attempt with optional deduplicated provider accounting.</summary>
public sealed class AiUsageRecord
{
    public Guid Id { get; set; }
    public required string Provider { get; set; }
    public required string Model { get; set; }
    public required string Operation { get; set; }
    public required string AttemptKey { get; set; }
    public Guid? WorkJobId { get; set; }
    public Guid? AccountId { get; set; }
    public Guid? SurvivorId { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string Outcome { get; set; } = "started";
    public string? ProviderRequestId { get; set; }
    public string? AccountingKey { get; set; }
    public Guid? DuplicateOfId { get; set; }
    public long? InputTokens { get; set; }
    public long? OutputTokens { get; set; }
    public long? TotalTokens { get; set; }
    public long? CachedTokens { get; set; }
    public long? TextInputTokens { get; set; }
    public long? ImageInputTokens { get; set; }
    public long? ImageOutputTokens { get; set; }
    public decimal? Credits { get; set; }
    public decimal? ComputeSeconds { get; set; }
    public decimal? Cost { get; set; }
    public string? Currency { get; set; }
}
/// <summary>Installation-wide start of reliable AI usage recording.</summary>
public sealed class AiUsageTrackingRecord
{
    public int Id { get; set; }
    public DateTimeOffset StartedAt { get; set; }
}
