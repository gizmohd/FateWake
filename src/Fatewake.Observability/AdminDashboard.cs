namespace Fatewake.Observability;
/// <summary>Filters shared by admin job, usage and calendar drill-down views.</summary>
public sealed class AdminDashboardFilter
{
    /// <summary>Time window: today, 7d, 30d, all, or custom.</summary>
    public string Range { get; set; } = "7d";
    /// <summary>Inclusive custom start instant.</summary>
    public DateTimeOffset? From { get; set; }
    /// <summary>Exclusive custom end instant.</summary>
    public DateTimeOffset? To { get; set; }
    /// <summary>Optional provider identity.</summary>
    public string? Provider { get; set; }
    /// <summary>Optional exact model identity.</summary>
    public string? Model { get; set; }
    /// <summary>Optional operation identity.</summary>
    public string? Operation { get; set; }
    /// <summary>Optional durable job status.</summary>
    public string? JobStatus { get; set; }
    /// <summary>Optional AI attempt outcome.</summary>
    public string? Outcome { get; set; }
    /// <summary>Optional attributed account filter.</summary>
    public Guid? AccountId { get; set; }
    /// <summary>Include only guest or unattributed activity.</summary>
    public bool Unattributed { get; set; }
    /// <summary>IANA timezone for calendar boundaries.</summary>
    public string TimeZone { get; set; } = "America/New_York";
    /// <summary>One-based result page.</summary>
    public int Page { get; set; } = 1;
    /// <summary>Bounded number of rows per page.</summary>
    public int PageSize { get; set; } = 25;
    /// <summary>Whitelisted sort column.</summary>
    public string Sort { get; set; } = "date";
    /// <summary>Whether to sort in descending order.</summary>
    public bool Descending { get; set; } = true;
    /// <summary>Calendar expansion level.</summary>
    public string Level { get; set; } = "year";
    /// <summary>Calendar key whose children are requested.</summary>
    public string? Parent { get; set; }
}
/// <summary>Reported counters; null totals indicate no available measurement.</summary>
public sealed record AiMetrics(int Calls,int Succeeded,int Failed,int Started,int Cancelled,int TimedOut,int DuplicateReports,
    int CallsWithTokens,long? InputTokens,long? OutputTokens,long? TotalTokens,long? CachedTokens,long? TextInputTokens,
    long? ImageInputTokens,long? ImageOutputTokens,decimal? Credits,decimal? ComputeSeconds);
/// <summary>Actual provider-reported cost in one currency.</summary>
public sealed record AiCurrencyCost(string Currency,decimal Amount);
/// <summary>One aggregate calendar row in the nested usage grid.</summary>
public sealed record AiCalendarGroup(string Key,AiMetrics Metrics,AiCurrencyCost[] Costs);
/// <summary>Headline statistics and recording coverage.</summary>
public sealed record AdminDashboardSummary(Dictionary<string,int> Jobs,int ImageVersions,int DistinctAssets,int ReusedJobs,
    AiMetrics Usage,AiCurrencyCost[] Costs,DateTimeOffset? TrackingStartedAt,int HistoricalUntrackedGenerations,DateTimeOffset UpdatedAt);
/// <summary>Safe user identity for Admin filtering.</summary>
public sealed record AdminDashboardUser(Guid Id,string Label);
/// <summary>Bounded result page.</summary>
public sealed record AdminResultPage<T>(T[] Items,int Total,int Page,int PageSize);
/// <summary>Safe generation job row.</summary>
public sealed record AdminJobRow(Guid Id,string AssetKey,string Provider,string Model,string Operation,string Status,
    Guid? AccountId,string User,string CurrentStep,int Attempts,DateTimeOffset CreatedAt,DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,string? Failure,int? AssetVersion);
/// <summary>Safe step details for a selected job.</summary>
public sealed record AdminJobStep(string Step,string Status,int Attempts,DateTimeOffset UpdatedAt,string? Failure);
/// <summary>Job details excluding prompts and execution payloads.</summary>
public sealed record AdminJobDetail(AdminJobRow Job,AdminJobStep[] Steps);
/// <summary>Individual external AI attempt and reported measurements.</summary>
public sealed record AdminUsageRow(Guid Id,Guid? WorkJobId,Guid? AccountId,Guid? SurvivorId,string User,string Provider,string Model,
    string Operation,string Outcome,DateTimeOffset StartedAt,DateTimeOffset? CompletedAt,string? ProviderRequestId,
    bool CountedElsewhere,long? InputTokens,long? OutputTokens,long? TotalTokens,long? CachedTokens,long? TextInputTokens,
    long? ImageInputTokens,long? ImageOutputTokens,decimal? Credits,decimal? ComputeSeconds,decimal? Cost,string? Currency);
