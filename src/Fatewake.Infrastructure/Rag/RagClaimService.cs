using Fatewake.Infrastructure.Authentication;
using Fatewake.Infrastructure.Persistence;
using Fatewake.Infrastructure.Story;
using Microsoft.EntityFrameworkCore;

namespace Fatewake.Infrastructure.Rag;

public sealed record RagClaimReview(Guid Id, string ClaimType, string Subject, string? Predicate, string? Object,
    decimal Confidence, string Status, string? Evidence, string SourceKey, string DocumentTitle, int ChunkOrdinal,
    string? ReviewedBy, DateTimeOffset? ReviewedAt);

public sealed record RagClaimPage(RagClaimReview[] Claims, bool HasMore);
public sealed record RagClaimDecision(string ExpectedStatus, string Status);
public sealed record RagClaimSource(Guid Id, string Key, string Title, decimal? MinimumConfidence);
public sealed record RagClaimSourcePolicy(decimal? MinimumConfidence);

public sealed class RagClaimService(FatewakeDbContext db)
{
    public const string Pending = "PendingReview";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    private const string AutomaticReviewer = "auto:source-policy";
    private const int PageSize = 20;

    private IQueryable<RagSourceRecord> ImportedSources() => db.RagSources.AsNoTracking()
        .Where(source => source.Origin == "admin-upload" && source.LicenseStatus == "Authorized" &&
            (source.Visibility == "Internal" || source.Visibility == "SharedApprovedCanon") &&
            (source.ReviewStatus == Approved || source.ReviewStatus == Pending));

    private IQueryable<ClaimSourceChunk> EligibleChunks() =>
        from source in ImportedSources()
        join document in db.RagDocuments.AsNoTracking() on source.Id equals document.SourceId
        join chunk in db.RagChunks.AsNoTracking() on document.Id equals chunk.DocumentId
        where document.Status == Approved && (chunk.ReviewStatus == Approved || chunk.ReviewStatus == Pending)
        select new ClaimSourceChunk { Source = source, Chunk = chunk };

    public async Task<bool> ExtractNextAsync(IClaimExtractor extractor, CancellationToken ct)
    {
        var leaseId = Guid.NewGuid();
        var reserved = await db.Database.SqlQuery<ReservedChunk>($"""
            WITH next AS (
                SELECT chunk."Id", source."Id" AS "SourceId"
                FROM rag_chunk chunk
                JOIN rag_document document ON document."Id" = chunk."DocumentId"
                JOIN rag_source source ON source."Id" = document."SourceId"
                WHERE source."Origin" = 'admin-upload' AND source."LicenseStatus" = 'Authorized'
                  AND source."Visibility" IN ('Internal', 'SharedApprovedCanon')
                  AND source."ReviewStatus" IN ('Approved', 'PendingReview') AND document."Status" = 'Approved'
                  AND chunk."ReviewStatus" IN ('Approved', 'PendingReview')
                  AND chunk."ClaimsExtractedAt" IS NULL
                  AND (chunk."ClaimExtractionLeaseUntil" IS NULL OR chunk."ClaimExtractionLeaseUntil" <= now())
                ORDER BY chunk."CreatedAt", chunk."Id"
                LIMIT 1
                FOR UPDATE OF chunk SKIP LOCKED
            )
            UPDATE rag_chunk chunk
            SET "ClaimExtractionLeaseId" = {leaseId}, "ClaimExtractionLeaseUntil" = now() + interval '5 minutes'
            FROM next WHERE chunk."Id" = next."Id"
            RETURNING chunk."Id", chunk."Text", chunk."DocumentId", next."SourceId"
            """).ToArrayAsync(ct);
        if (reserved.Length == 0)
            return false;
        var next = reserved[0];

        var candidates = await extractor.ExtractAsync(next.Text, ct);
        var facts = candidates.Where(claim => IsSupported(claim, next.Text))
            .DistinctBy(claim => (claim.Subject.Trim().ToUpperInvariant(), claim.Predicate.Trim().ToUpperInvariant(),
                claim.Object.Trim().ToUpperInvariant())).Take(6).ToArray();
        if (candidates.Length > 0 && facts.Length == 0)
            throw new InvalidDataException("Claim extraction returned no verifiable evidence.");

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Serialize against source policy changes before publishing either pending or auto-approved claims.
        await db.RagSources.FromSqlInterpolated($"""
            SELECT * FROM rag_source WHERE "Id" = {next.SourceId} FOR SHARE
            """).AsNoTracking().ToListAsync(ct);
        var source = await EligibleChunks().Where(row => row.Chunk.Id == next.Id)
            .Select(row => new { row.Source.ClaimAutoApprovalMinimumConfidence }).SingleOrDefaultAsync(ct);
        if (source is null)
            return true;

        var now = DateTimeOffset.UtcNow;
        var completed = await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE rag_chunk
            SET "ClaimsExtractedAt" = {now}, "ClaimExtractionLeaseId" = NULL, "ClaimExtractionLeaseUntil" = NULL
            WHERE "Id" = {next.Id} AND "ClaimsExtractedAt" IS NULL AND "ClaimExtractionLeaseId" = {leaseId}
            """, ct);
        if (completed == 0)
            return true;

        foreach (var claim in facts)
        {
            var confidence = decimal.Round(claim.Confidence, 4);
            var auto = source.ClaimAutoApprovalMinimumConfidence is { } minimum && confidence >= minimum;
            db.RagClaims.Add(new RagClaimRecord
            {
                Id = Guid.NewGuid(),
                SourceChunkId = next.Id,
                SourceDocumentId = next.DocumentId,
                ClaimType = claim.ClaimType.Trim(),
                Subject = claim.Subject.Trim(),
                Predicate = claim.Predicate.Trim(),
                Object = claim.Object.Trim(),
                Evidence = claim.Evidence.Trim(),
                Confidence = confidence,
                Status = auto ? Approved : Pending,
                ApprovedBy = auto ? AutomaticReviewer : null,
                ApprovedAt = auto ? now : null,
                ReviewedBy = auto ? AutomaticReviewer : null,
                ReviewedAt = auto ? now : null,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return true;
    }

    private static bool IsSupported(ExtractedClaim? claim, string passage) =>
        claim is not null && claim.Confidence is >= 0 and <= 1 &&
        Allowed(claim.ClaimType, 80) && Allowed(claim.Subject, 160) &&
        Allowed(claim.Predicate, 160) && Allowed(claim.Object, 400) && Allowed(claim.Evidence, 700) &&
        passage.Contains(claim.Evidence.Trim(), StringComparison.Ordinal);

    private static bool Allowed(string? value, int limit) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= limit &&
        StoryContentPolicy.Allows(ContentLevel.AdultClean, value);

    public static bool IsReviewStatus(string? status) => status is Pending or Approved or Rejected;

    public async Task<RagClaimPage> ListAsync(string status, int page, CancellationToken ct)
    {
        var claims = await (
            from claim in db.RagClaims.AsNoTracking()
            join chunk in db.RagChunks.AsNoTracking() on claim.SourceChunkId equals chunk.Id
            join document in db.RagDocuments.AsNoTracking() on chunk.DocumentId equals document.Id
            join source in db.RagSources.AsNoTracking() on document.SourceId equals source.Id
            where claim.Status == status
            orderby claim.CreatedAt, claim.Id
            select new RagClaimReview(claim.Id, claim.ClaimType, claim.Subject, claim.Predicate, claim.Object,
                claim.Confidence, claim.Status, claim.Evidence, source.SourceKey, document.Title, chunk.Ordinal,
                claim.ReviewedBy, claim.ReviewedAt))
            .Skip((page - 1) * PageSize).Take(PageSize + 1).ToArrayAsync(ct);
        return new(claims.Take(PageSize).ToArray(), claims.Length > PageSize);
    }

    public Task<int> ReviewAsync(Guid id, RagClaimDecision decision, string reviewer, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var approve = decision.Status == Approved;
        var query = db.RagClaims.Where(claim => claim.Id == id && claim.Status == decision.ExpectedStatus);
        if (approve)
            query = query.Where(claim => claim.Evidence != null &&
                EligibleChunks().Any(row => row.Chunk.Id == claim.SourceChunkId));
        return query.ExecuteUpdateAsync(update => update.SetProperty(claim => claim.Status, decision.Status)
            .SetProperty(claim => claim.ReviewedBy, reviewer).SetProperty(claim => claim.ReviewedAt, now)
            .SetProperty(claim => claim.ApprovedBy, approve ? reviewer : null)
            .SetProperty(claim => claim.ApprovedAt, approve ? now : (DateTimeOffset?)null)
            .SetProperty(claim => claim.UpdatedAt, now), ct);
    }

    public Task<RagClaimSource[]> ListSourcesAsync(string? search, CancellationToken ct)
    {
        var sources = ImportedSources();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            sources = sources.Where(source => source.SourceKey.Contains(term) || source.Title.Contains(term));
        }
        return sources.OrderBy(source => source.SourceKey).Take(100)
            .Select(source => new RagClaimSource(source.Id, source.SourceKey, source.Title,
                source.ClaimAutoApprovalMinimumConfidence)).ToArrayAsync(ct);
    }

    public async Task<bool> SetPolicyAsync(Guid sourceId, decimal? minimum, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var changed = await ImportedSources().Where(source => source.Id == sourceId)
            .ExecuteUpdateAsync(update => update.SetProperty(source => source.ClaimAutoApprovalMinimumConfidence, minimum)
                .SetProperty(source => source.UpdatedAt, DateTimeOffset.UtcNow), ct);
        if (changed == 0)
            return false;

        if (minimum is { } threshold)
        {
            var now = DateTimeOffset.UtcNow;
            await db.RagClaims.Where(claim => claim.Status == Pending && claim.Evidence != null &&
                claim.Confidence >= threshold && EligibleChunks().Any(row => row.Source.Id == sourceId &&
                    row.Chunk.Id == claim.SourceChunkId))
                .ExecuteUpdateAsync(update => update.SetProperty(claim => claim.Status, Approved)
                    .SetProperty(claim => claim.ApprovedBy, AutomaticReviewer).SetProperty(claim => claim.ApprovedAt, now)
                    .SetProperty(claim => claim.ReviewedBy, AutomaticReviewer).SetProperty(claim => claim.ReviewedAt, now)
                    .SetProperty(claim => claim.UpdatedAt, now), ct);
        }

        await transaction.CommitAsync(ct);
        return true;
    }

    public Task<string[]> SearchApprovedAsync(string query, RagSearchFilter filter, int limit, CancellationToken ct)
    {
        var types = filter.SourceTypes?.ToArray();
        return db.Database.SqlQuery<string>($"""
            WITH search AS (
                SELECT to_tsquery('english', string_agg(quote_literal(word), ' | ')) AS terms
                FROM unnest(tsvector_to_array(to_tsvector('english', {query}))) AS word
            )
            SELECT concat_ws(' ', claim."Subject", claim."Predicate", claim."Object") AS "Value"
            FROM rag_claim claim
            JOIN rag_chunk chunk ON chunk."Id" = claim."SourceChunkId"
            JOIN rag_document document ON document."Id" = chunk."DocumentId"
            JOIN rag_source source ON source."Id" = document."SourceId"
            CROSS JOIN search
            CROSS JOIN LATERAL (SELECT to_tsvector('english', claim."Subject" || ' ' ||
                coalesce(claim."Predicate", '') || ' ' || coalesce(claim."Object", '')) AS terms) AS fact
            WHERE claim."Status" = 'Approved' AND claim."Evidence" IS NOT NULL
              AND source."Origin" = 'admin-upload' AND source."LicenseStatus" = 'Authorized'
              AND source."Visibility" IN ('SharedApprovedCanon', 'Internal')
              AND source."ReviewStatus" IN ('Approved', 'PendingReview') AND document."Status" = 'Approved'
              AND chunk."ReviewStatus" IN ('Approved', 'PendingReview')
              AND (chunk."TimelineKey" IS NULL OR chunk."TimelineKey" = {filter.TimelineKey})
              AND ({filter.SpoilerSeriesMax}::int IS NULL OR chunk."SpoilerSeriesMin" IS NULL OR chunk."SpoilerSeriesMin" <= {filter.SpoilerSeriesMax})
              AND ({filter.SurvivorDayMax}::int IS NULL OR chunk."SurvivorDayMin" IS NULL OR chunk."SurvivorDayMin" <= {filter.SurvivorDayMax})
              AND ({types}::text[] IS NULL OR source."SourceType" = ANY({types}))
              AND fact.terms @@ search.terms
            ORDER BY ts_rank(fact.terms, search.terms) DESC, claim."Id"
            LIMIT {Math.Clamp(limit, 1, 50)}
            """).ToArrayAsync(ct);
    }

    private sealed record ReservedChunk(Guid Id, string Text, Guid DocumentId, Guid SourceId);
    private sealed class ClaimSourceChunk
    {
        public required RagSourceRecord Source { get; init; }
        public required RagChunkRecord Chunk { get; init; }
    }
}
