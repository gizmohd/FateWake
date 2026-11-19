using Fatewake.Infrastructure.Persistence;
using Fatewake.Observability;
using Microsoft.EntityFrameworkCore;
namespace Fatewake.Infrastructure.Usage;
/// <summary>Persists all attempts and merges repeated reports of one provider execution.</summary>
public sealed class AiUsageRecorder(FatewakeDbContext db) : IAiUsageRecorder
{
    /// <inheritdoc />
    public async Task<Guid> BeginAsync(AiCallContext context, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockAsync(context.Provider + ":" + context.AttemptKey, ct);
        var prior = await db.Set<AiUsageRecord>().SingleOrDefaultAsync(x => x.Provider == context.Provider && x.AttemptKey == context.AttemptKey, ct);
        if (prior is not null)
            return prior.Id;
        var row = new AiUsageRecord
        {
            Id = Guid.NewGuid(),
            Provider = context.Provider,
            Model = context.Model,
            Operation = context.Operation,
            AttemptKey = context.AttemptKey,
            WorkJobId = context.WorkJobId,
            AccountId = context.AccountId,
            SurvivorId = context.SurvivorId,
            StartedAt = DateTimeOffset.UtcNow
        };
        db.Add(row);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return row.Id;
    }
    /// <inheritdoc />
    public async Task ReportAsync(Guid attemptId, AiUsageReport report, CancellationToken ct = default)
    {
        var row = await db.Set<AiUsageRecord>().SingleAsync(x => x.Id == attemptId, ct);
        var accounting = (report.ProviderRequestId ?? row.ProviderRequestId) is { } providerId ? "request:" + providerId : "attempt:" + attemptId;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockAsync(row.Provider + ":" + accounting, ct);
        await db.Entry(row).ReloadAsync(ct);
        var owner = await db.Set<AiUsageRecord>().SingleOrDefaultAsync(x => x.Provider == row.Provider && x.AccountingKey == accounting, ct) ?? row;
        if (owner.Id == row.Id)
            row.DuplicateOfId = null;
        row.ProviderRequestId = report.ProviderRequestId ?? row.ProviderRequestId;
        if (row.WorkJobId is { } job && row.ProviderRequestId is { } providerJob)
        {
            var generation = await db.ArtGenerations.SingleOrDefaultAsync(x => x.WorkJobId == job, ct);
            if (generation is not null)
                generation.ProviderJobId = providerJob;
        }
        owner.AccountingKey = accounting;
        owner.ProviderRequestId = report.ProviderRequestId ?? owner.ProviderRequestId;
        owner.InputTokens = Valid(report.InputTokens) ?? owner.InputTokens;
        owner.OutputTokens = Valid(report.OutputTokens) ?? owner.OutputTokens;
        owner.TotalTokens = Valid(report.TotalTokens) ?? owner.TotalTokens;
        owner.CachedTokens = Valid(report.CachedTokens) ?? owner.CachedTokens;
        owner.TextInputTokens = Valid(report.TextInputTokens) ?? owner.TextInputTokens;
        owner.ImageInputTokens = Valid(report.ImageInputTokens) ?? owner.ImageInputTokens;
        owner.ImageOutputTokens = Valid(report.ImageOutputTokens) ?? owner.ImageOutputTokens;
        owner.Credits = Valid(report.Credits) ?? owner.Credits;
        owner.ComputeSeconds = Valid(report.ComputeSeconds) ?? owner.ComputeSeconds;
        if (Valid(report.Cost) is { } cost && report.Currency is { Length: 3 } currency && currency.All(char.IsAsciiLetter))
        {
            owner.Cost = cost;
            owner.Currency = currency.ToUpperInvariant();
        }
        if (owner.Id != row.Id)
        {
            row.DuplicateOfId = owner.Id;
            row.AccountingKey = null;
            row.InputTokens = row.OutputTokens = row.TotalTokens = row.CachedTokens = row.TextInputTokens = row.ImageInputTokens = row.ImageOutputTokens = null;
            row.Credits = row.ComputeSeconds = row.Cost = null;
            row.Currency = null;
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
    /// <inheritdoc />
    public async Task FinishAsync(Guid attemptId, string outcome, CancellationToken ct = default)
    {
        if (outcome is not ("succeeded" or "failed" or "timed_out" or "cancelled"))
            throw new ArgumentException("Invalid AI outcome.");
        var row = await db.Set<AiUsageRecord>().SingleAsync(x => x.Id == attemptId, ct);
        row.Outcome = outcome;
        row.CompletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }
    private Task<int> LockAsync(string key, CancellationToken ct) => db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({"ai:" + key},0))", ct);
    private static long? Valid(long? value) => value is >= 0 ? value : null;
    private static decimal? Valid(decimal? value) => value is >= 0 ? value : null;
}
