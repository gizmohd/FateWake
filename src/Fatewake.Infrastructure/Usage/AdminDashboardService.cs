using System.Globalization;
using System.Linq.Expressions;
using System.Text.Json;
using Fatewake.Infrastructure.Art;
using Fatewake.Infrastructure.Persistence;
using Fatewake.Observability;
using Microsoft.EntityFrameworkCore;
namespace Fatewake.Infrastructure.Usage;
/// <summary>Server-side bounded, read-only operational and accounting projections.</summary>
public sealed class AdminDashboardService(FatewakeDbContext db)
{
    /// <summary>Checks query bounds before executing database work.</summary>
    public static bool Valid(AdminDashboardFilter f)
    {
        if (f.Page < 1 || f.Page > 100000 || f.PageSize is < 1 or > 100 || f.Range is not ("7d" or "today" or "30d" or "all" or "custom") ||
            f.Sort is not ("date" or "user" or "provider" or "model" or "status" or "tokens" or "asset" or "duration") ||
            f.Level is not ("year" or "month" or "day" or "request") || f.Provider?.Length > 100 || f.Model?.Length > 200 ||
            f.Operation?.Length > 100 || f.AccountId is not null && f.Unattributed) return false;
        if (f.Range == "custom" && (f.From is null || f.To is null || f.From >= f.To)) return false;
        if (f.JobStatus is { } job && (!Enum.TryParse<WorkJobStatus>(job, true, out var jobState) || !Enum.IsDefined(jobState))) return false;
        if (f.Outcome is { } outcome && outcome is not ("started" or "succeeded" or "failed" or "timed_out" or "cancelled")) return false;
        try { if (f.TimeZone != "UTC" && !TimeZoneInfo.TryConvertIanaIdToWindowsId(f.TimeZone, out _)) return false; _ = TimeZoneInfo.FindSystemTimeZoneById(f.TimeZone); }
        catch (TimeZoneNotFoundException) { return false; }
        var pattern = f.Level switch { "month" => "yyyy", "day" => "yyyy-MM", "request" => "yyyy-MM-dd", _ => null };
        return pattern is null ? f.Parent is null : f.Parent is null && f.Level == "request" ||
            DateTime.TryParseExact(f.Parent, pattern, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
    }
    /// <summary>Projects headline totals without loading individual usage records.</summary>
    public async Task<AdminDashboardSummary> SummaryAsync(AdminDashboardFilter f, CancellationToken ct = default)
    {
        var jobs = Jobs(f); var usage = Usage(f);
        var statuses = await jobs.GroupBy(x => x.Status).Select(g => new { g.Key, Count = g.Count() }).ToArrayAsync(ct);
        var metrics = (await usage.GroupBy(x => "all").Select(Buckets).SingleOrDefaultAsync(ct))?.Metrics ?? Empty;
        var costs = await CostsAsync(usage, ct);
        var assets = Assets(f);
        var versions = await assets.CountAsync(ct); var distinct = await assets.Select(x => x.Key).Distinct().CountAsync(ct);
        var reuse = await db.WorkArtifacts.Where(x => x.Key == "art.master" && EF.Functions.JsonContains(x.Value, "{\"Reused\":true}") &&
            jobs.Any(j => j.Id == x.JobId && j.Status == WorkJobStatus.Completed)).CountAsync(ct);
        var tracking = await db.Set<AiUsageTrackingRecord>().Where(x => x.Id == 1).Select(x => (DateTimeOffset?)x.StartedAt).SingleOrDefaultAsync(ct);
        var untracked = await db.ArtGenerations.Where(x => !db.Set<AiUsageRecord>().Any(u => u.WorkJobId == x.WorkJobId) &&
            jobs.Any(j => j.Id == x.WorkJobId)).CountAsync(ct);
        return new(statuses.ToDictionary(x => x.Key.ToString(), x => x.Count), versions, distinct, reuse, metrics, costs, tracking, untracked, DateTimeOffset.UtcNow);
    }
    /// <summary>Expands one calendar level, keeping aggregates in PostgreSQL.</summary>
    public async Task<AiCalendarGroup[]> GroupsAsync(AdminDashboardFilter f, CancellationToken ct = default)
    {
        var pattern = f.Level switch { "year" => "YYYY", "month" => "YYYY-MM", _ => "YYYY-MM-DD" };
        var usage = Usage(f);
        var buckets = usage.GroupBy(x => FatewakeDbContext.CalendarKey(x.StartedAt, f.TimeZone, pattern)).Select(Buckets);
        var sorted = f.Sort == "tokens" ? f.Descending ? buckets.OrderByDescending(x => x.Tokens) : buckets.OrderBy(x => x.Tokens)
            : f.Descending ? buckets.OrderByDescending(x => x.Key) : buckets.OrderBy(x => x.Key);
        var rows = await sorted.Take(500).ToArrayAsync(ct);
        var costs = await usage.Where(x => x.Cost != null && x.Currency != null).GroupBy(x => new { Key = FatewakeDbContext.CalendarKey(x.StartedAt, f.TimeZone, pattern), x.Currency })
            .Select(g => new { g.Key.Key, g.Key.Currency, Amount = g.Sum(x => x.Cost!.Value) }).ToArrayAsync(ct);
        return rows.Select(x => new AiCalendarGroup(x.Key, x.Metrics, costs.Where(c => c.Key == x.Key).Select(c => new AiCurrencyCost(c.Currency!, c.Amount)).ToArray())).ToArray();
    }
    /// <summary>Returns a bounded, sortable page of individual AI attempts.</summary>
    public async Task<AdminResultPage<AdminUsageRow>> UsageAsync(AdminDashboardFilter f, CancellationToken ct = default)
    {
        var q = Usage(f); var count = await q.CountAsync(ct);
        Expression<Func<AiUsageRecord, object?>> sort = f.Sort switch
        {
            "user" => x => db.Accounts.Where(a => a.Id == x.AccountId).Select(a => a.PrimaryEmail ?? a.DisplayName).FirstOrDefault(),
            "provider" => x => x.Provider,
            "model" => x => x.Model,
            "status" => x => x.Outcome,
            "tokens" => x => x.TotalTokens,
            "duration" => x => x.CompletedAt - x.StartedAt,
            _ => x => x.StartedAt
        };
        var sorted = f.Descending ? q.OrderByDescending(sort) : q.OrderBy(sort);
        var rows = await sorted.ThenBy(x => x.Id).Skip((f.Page - 1) * f.PageSize).Take(f.PageSize).ToArrayAsync(ct);
        var users = await LabelsAsync(rows.Select(x => x.AccountId), ct);
        return new(rows.Select(x => new AdminUsageRow(x.Id, x.WorkJobId, x.AccountId, x.SurvivorId, Label(users, x.AccountId), x.Provider, x.Model, x.Operation,
            x.Outcome, x.StartedAt, x.CompletedAt, x.ProviderRequestId, x.DuplicateOfId != null, x.InputTokens, x.OutputTokens, x.TotalTokens, x.CachedTokens,
            x.TextInputTokens, x.ImageInputTokens, x.ImageOutputTokens, x.Credits, x.ComputeSeconds, x.Cost, x.Currency)).ToArray(), count, f.Page, f.PageSize);
    }
    /// <summary>Returns a bounded job page including safe processing state and approved previews.</summary>
    public async Task<AdminResultPage<AdminJobRow>> JobsAsync(AdminDashboardFilter f, CancellationToken ct = default)
    {
        var q = Jobs(f); var count = await q.CountAsync(ct);
        Expression<Func<WorkJobRecord, object?>> sort = f.Sort switch
        {
            "user" => x => db.Accounts.Where(a => a.Id.ToString() == FatewakeDbContext.JsonText(x.Payload, "AccountId")).Select(a => a.PrimaryEmail ?? a.DisplayName).FirstOrDefault(),
            "provider" => x => FatewakeDbContext.JsonText(x.Payload, "Provider"),
            "model" => x => FatewakeDbContext.JsonText(x.Payload, "Model"),
            "asset" => x => FatewakeDbContext.JsonText(x.Payload, "AssetKey"),
            "status" => x => x.Status,
            "duration" => x => x.CompletedAt - x.CreatedAt,
            _ => x => x.CreatedAt
        };
        var sorted = f.Descending ? q.OrderByDescending(sort) : q.OrderBy(sort);
        var rows = await sorted.ThenBy(x => x.Id).Skip((f.Page - 1) * f.PageSize).Take(f.PageSize).ToArrayAsync(ct);
        return new(await JobRowsAsync(rows, ct), count, f.Page, f.PageSize);
    }
    /// <summary>Shows safe details for one job without executing it.</summary>
    public async Task<AdminJobDetail?> JobAsync(Guid id, CancellationToken ct = default)
    {
        var job = await db.WorkJobs.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.JobType == "art.asset", ct);
        if (job is null) return null;
        var row = (await JobRowsAsync([job], ct))[0];
        var steps = await db.WorkSteps.AsNoTracking().Where(x => x.JobId == id).OrderBy(x => x.CreatedAt).ThenBy(x => x.StepType).ToArrayAsync(ct);
        return new(row, steps.Select(x => new AdminJobStep(x.StepType, x.Status.ToString(), x.AttemptCount, x.UpdatedAt, Failure(x))).ToArray());
    }
    /// <summary>Searches bounded account labels for filtering, including accounts with no recorded AI activity.</summary>
    public Task<AdminDashboardUser[]> UsersAsync(string? search, CancellationToken ct = default)
    {
        var q = db.Accounts.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search)) q = q.Where(x => (x.PrimaryEmail ?? "").Contains(search) || (x.DisplayName ?? "").Contains(search));
        return q.OrderBy(x => x.PrimaryEmail ?? x.DisplayName).Take(50).Select(x => new AdminDashboardUser(x.Id, x.PrimaryEmail ?? x.DisplayName ?? x.Id.ToString())).ToArrayAsync(ct);
    }
    private IQueryable<AiUsageRecord> Usage(AdminDashboardFilter f)
    {
        var q = db.Set<AiUsageRecord>().AsNoTracking(); var (from, to) = Bounds(f);
        if (from is { } start) q = q.Where(x => x.StartedAt >= start); if (to is { } end) q = q.Where(x => x.StartedAt < end);
        if (f.Provider is { } provider) q = q.Where(x => x.Provider == provider); if (f.Model is { } model) q = q.Where(x => x.Model == model);
        if (f.Operation is { } op) q = q.Where(x => x.Operation == op); if (f.Outcome is { } outcome) q = q.Where(x => x.Outcome == outcome);
        if (f.AccountId is { } user) q = q.Where(x => x.AccountId == user); if (f.Unattributed) q = q.Where(x => x.AccountId == null);
        if (f.Parent is { } parent) { var pattern = f.Level switch { "month" => "YYYY", "day" => "YYYY-MM", _ => "YYYY-MM-DD" }; q = q.Where(x => FatewakeDbContext.CalendarKey(x.StartedAt, f.TimeZone, pattern) == parent); }
        return q;
    }
    private IQueryable<WorkJobRecord> Jobs(AdminDashboardFilter f, bool dates = true)
    {
        var q = db.WorkJobs.AsNoTracking().Where(x => x.JobType == "art.asset"); var (from, to) = Bounds(f);
        if (dates && from is { } start) q = q.Where(x => x.CreatedAt >= start); if (dates && to is { } end) q = q.Where(x => x.CreatedAt < end);
        if (f.Provider is { } provider) q = q.Where(x => FatewakeDbContext.JsonText(x.Payload, "Provider") == provider);
        if (f.Model is { } model) q = q.Where(x => FatewakeDbContext.JsonText(x.Payload, "Model") == model);
        if (f.Operation is { } op)
        {
            // New jobs carry an explicit operation; legacy jobs without references are normal generations.
            q = q.Where(x => (FatewakeDbContext.JsonText(x.Payload, "Operation") ??
                (FatewakeDbContext.JsonText(x.Payload, "ReferenceImages") != null && FatewakeDbContext.JsonText(x.Payload, "ReferenceImages") != "[]" ? "image.edit" : "image.generate")) == op);
        }
        if (f.AccountId is { } account) { var id = account.ToString(); q = q.Where(x => FatewakeDbContext.JsonText(x.Payload, "AccountId") == id); }
        if (f.Unattributed) q = q.Where(x => FatewakeDbContext.JsonText(x.Payload, "AccountId") == null);
        if (f.JobStatus is { } status && Enum.TryParse<WorkJobStatus>(status, true, out var state)) q = q.Where(x => x.Status == state);
        return q;
    }
    private IQueryable<ArtAssetRecord> Assets(AdminDashboardFilter f)
    {
        var q = db.ArtAssets.AsNoTracking(); var (from, to) = Bounds(f);
        if (from is { } start) q = q.Where(x => x.CreatedAt >= start); if (to is { } end) q = q.Where(x => x.CreatedAt < end);
        if (f.Provider != null || f.Model != null || f.Operation != null || f.AccountId != null || f.Unattributed || f.JobStatus != null)
        { var jobs = Jobs(f, false); q = q.Where(x => db.ArtGenerations.Any(g => g.VisualFingerprint == x.VisualFingerprint && jobs.Any(j => j.Id == g.WorkJobId))); }
        return q;
    }
    private async Task<AdminJobRow[]> JobRowsAsync(WorkJobRecord[] jobs, CancellationToken ct)
    {
        var ids = jobs.Select(x => x.Id).ToArray();
        var requests = jobs.ToDictionary(x => x.Id, x => JsonSerializer.Deserialize<ArtWorkRequest>(x.Payload)!);
        var steps = await db.WorkSteps.AsNoTracking().Where(x => ids.Contains(x.JobId)).ToArrayAsync(ct);
        var starts = await db.Set<AiUsageRecord>().Where(x => x.WorkJobId != null && ids.Contains(x.WorkJobId.Value)).GroupBy(x => x.WorkJobId!.Value)
            .Select(g => new { g.Key, Start = g.Min(x => x.StartedAt) }).ToDictionaryAsync(x => x.Key, x => x.Start, ct);
        var fingerprints = requests.Values.Select(x => x.VisualFingerprint).ToArray();
        var assets = await db.ArtAssets.AsNoTracking().Where(x => fingerprints.Contains(x.VisualFingerprint) && x.Status == ArtAssetStatus.Approved)
            .Select(x => new { x.VisualFingerprint, x.Version }).ToArrayAsync(ct);
        var users = await LabelsAsync(requests.Values.Select(x => x.AccountId), ct);
        return jobs.Select(job =>
        {
            var request = requests[job.Id]; var parts = steps.Where(x => x.JobId == job.Id).ToArray();
            var active = parts.Where(x => x.Status is WorkStepStatus.Leased or WorkStepStatus.Retry or WorkStepStatus.Ready).Select(x => x.StepType).Distinct();
            return new AdminJobRow(job.Id, request.AssetKey, request.Provider, request.Model, request.Operation, job.Status.ToString(), request.AccountId, Label(users, request.AccountId),
                string.Join(", ", active), parts.Where(x => x.StepType == ArtWorkStepTypes.GenerateMaster).Select(x => x.AttemptCount).DefaultIfEmpty().Max(), job.CreatedAt,
                starts.TryGetValue(job.Id, out var start) ? start : null, job.CompletedAt, parts.Select(Failure).FirstOrDefault(x => x != null),
                assets.Where(x => x.VisualFingerprint == request.VisualFingerprint).Select(x => (int?)x.Version).Max());
        }).ToArray();
    }
    private Task<Dictionary<Guid, string>> LabelsAsync(IEnumerable<Guid?> ids, CancellationToken ct)
    { var keys = ids.Where(x => x != null).Select(x => x!.Value).Distinct().ToArray(); return db.Accounts.Where(x => keys.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.PrimaryEmail ?? x.DisplayName ?? x.Id.ToString(), ct); }
    private static string Label(Dictionary<Guid, string> labels, Guid? id) => id is { } account ? labels.GetValueOrDefault(account, account.ToString()) : "Unattributed / guest";
    private static string? Failure(WorkStepRecord step) => step.Status is WorkStepStatus.Failed or WorkStepStatus.Retry ? step.ErrorCode == "handler_missing" ? "Worker handler unavailable." : "Processing failed. Use the job ID to inspect server logs." : null;
    private static Task<AiCurrencyCost[]> CostsAsync(IQueryable<AiUsageRecord> q, CancellationToken ct) => q.Where(x => x.Cost != null && x.Currency != null)
        .GroupBy(x => x.Currency!).Select(g => new AiCurrencyCost(g.Key, g.Sum(x => x.Cost!.Value))).ToArrayAsync(ct);
    private static (DateTimeOffset? From, DateTimeOffset? To) Bounds(AdminDashboardFilter f)
    {
        var now = DateTimeOffset.UtcNow;
        return f.Range switch
        {
            "all" => (null, null),
            "custom" => (f.From!.Value.ToUniversalTime(), f.To!.Value.ToUniversalTime()),
            "today" => (new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(f.TimeZone)).Date, TimeZoneInfo.FindSystemTimeZoneById(f.TimeZone))), now),
            "30d" => (now.AddDays(-30), now),
            _ => (now.AddDays(-7), now)
        };
    }
    private sealed class UsageBucket
    {
        public required string Key { get; init; }
        public required AiMetrics Metrics { get; init; }
        public long? Tokens { get; init; }
    }
    private static readonly AiMetrics Empty = new(0, 0, 0, 0, 0, 0, 0, 0, null, null, null, null, null, null, null, null, null);
    private static readonly Expression<Func<IGrouping<string, AiUsageRecord>, UsageBucket>> Buckets = g => new UsageBucket
    {
        Key = g.Key,
        Tokens = g.Any(x => x.TotalTokens != null) ? g.Sum(x => x.TotalTokens) : null,
        Metrics = new AiMetrics(g.Count(), g.Count(x => x.Outcome == "succeeded"), g.Count(x => x.Outcome == "failed"), g.Count(x => x.Outcome == "started"),
            g.Count(x => x.Outcome == "cancelled"), g.Count(x => x.Outcome == "timed_out"), g.Count(x => x.DuplicateOfId != null), g.Count(x => x.InputTokens != null || x.OutputTokens != null || x.TotalTokens != null),
            g.Any(x => x.InputTokens != null) ? g.Sum(x => x.InputTokens) : null, g.Any(x => x.OutputTokens != null) ? g.Sum(x => x.OutputTokens) : null,
            g.Any(x => x.TotalTokens != null) ? g.Sum(x => x.TotalTokens) : null, g.Any(x => x.CachedTokens != null) ? g.Sum(x => x.CachedTokens) : null,
            g.Any(x => x.TextInputTokens != null) ? g.Sum(x => x.TextInputTokens) : null, g.Any(x => x.ImageInputTokens != null) ? g.Sum(x => x.ImageInputTokens) : null,
            g.Any(x => x.ImageOutputTokens != null) ? g.Sum(x => x.ImageOutputTokens) : null, g.Any(x => x.Credits != null) ? g.Sum(x => x.Credits) : null,
            g.Any(x => x.ComputeSeconds != null) ? g.Sum(x => x.ComputeSeconds) : null)
    };
}
