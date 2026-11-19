using Fatewake.Infrastructure.Persistence;
using Fatewake.Infrastructure.Story;
using Fatewake.Observability;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fatewake.Infrastructure.Rag;

/// <summary>Ranks authorized chunks by pgvector similarity, falling back to PostgreSQL full-text search.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Rag/RagRetriever.md">Documentation</see>.</remarks>
public sealed class RagRetriever(FatewakeDbContext db, IEmbeddingProvider provider, EmbeddingOptions options,
    ILogger<RagRetriever> log) : IRagRetriever
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<RagPassage>> SearchAsync(string query, RagSearchFilter filter, int limit,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];
        limit = Math.Clamp(limit, 1, 50);
        using var operation = OperationTelemetry.Start("rag.retrieve", log);
        try
        {
            var passages = Array.Empty<RagPassage>();
            if (options.Enabled && !string.IsNullOrWhiteSpace(options.ApiKey))
            {
                var vector = (await provider.EmbedAsync([query], null, filter.SurvivorId, ct))[0];
                passages = await SearchByVectorAsync(RagEmbeddingService.FormatVector(vector), filter, limit, ct);
            }
            if (passages.Length == 0)
                passages = await SearchByTextAsync(query, filter, limit, ct);
            log.LogInformation("Retrieved {PassageCount} RAG passages", passages.Length);
            return passages;
        }
        catch (OperationCanceledException ex) when (ct.IsCancellationRequested)
        {
            operation.Fail(ex);
            throw;
        }
        catch (Exception ex)
        {
            operation.Fail(ex);
            log.LogWarning(ex, "RAG retrieval failed");
            throw;
        }
    }

    // Shared canon and internal sources are visible to every survivor. Private generated story is limited to its owning
    // survivor and timeline. The scene worker reads generated Thread canon separately with sequence and content-level limits.
    // Spoiler and survivor-day limits apply to every source.
    private Task<RagPassage[]> SearchByVectorAsync(string vector, RagSearchFilter f, int limit, CancellationToken ct)
    {
        var survivor = f.SurvivorId?.ToString();
        var types = f.SourceTypes?.ToArray();
        return db.Database.SqlQuery<RagPassage>($"""
            SELECT c."Id" AS "ChunkId", s."SourceKey", s."SourceType", d."Title", c."Text",
                   1 - (c."Embedding" <=> {vector}::vector) AS "Score"
            FROM rag_chunk c
            JOIN rag_document d ON d."Id" = c."DocumentId"
            JOIN rag_source s ON s."Id" = d."SourceId"
            WHERE c."Embedding" IS NOT NULL
              AND c."ReviewStatus" = 'Approved' AND s."ReviewStatus" = 'Approved' AND d."Status" = 'Approved'
              AND s."SourceType" <> {ThreadCanon.SourceType}
              AND (s."Visibility" IN ('SharedApprovedCanon', 'Internal')
                   OR (s."Visibility" = 'Private' AND s."Metadata"->>'survivorId' = {survivor}
                       AND c."TimelineKey" = {f.TimelineKey}))
              AND ({f.SpoilerSeriesMax}::int IS NULL OR c."SpoilerSeriesMin" IS NULL OR c."SpoilerSeriesMin" <= {f.SpoilerSeriesMax})
              AND ({f.SurvivorDayMax}::int IS NULL OR c."SurvivorDayMin" IS NULL OR c."SurvivorDayMin" <= {f.SurvivorDayMax})
              AND ({types}::text[] IS NULL OR s."SourceType" = ANY({types}))
            ORDER BY c."Embedding" <=> {vector}::vector
            LIMIT {limit}
            """).ToArrayAsync(ct);
    }

    private Task<RagPassage[]> SearchByTextAsync(string query, RagSearchFilter f, int limit, CancellationToken ct)
    {
        var survivor = f.SurvivorId?.ToString();
        var types = f.SourceTypes?.ToArray();
        return db.Database.SqlQuery<RagPassage>($"""
            SELECT c."Id" AS "ChunkId", s."SourceKey", s."SourceType", d."Title", c."Text",
                   ts_rank(c."SearchVector", websearch_to_tsquery('english', {query}))::float8 AS "Score"
            FROM rag_chunk c
            JOIN rag_document d ON d."Id" = c."DocumentId"
            JOIN rag_source s ON s."Id" = d."SourceId"
            WHERE c."SearchVector" @@ websearch_to_tsquery('english', {query})
              AND c."ReviewStatus" = 'Approved' AND s."ReviewStatus" = 'Approved' AND d."Status" = 'Approved'
              AND s."SourceType" <> {ThreadCanon.SourceType}
              AND (s."Visibility" IN ('SharedApprovedCanon', 'Internal')
                   OR (s."Visibility" = 'Private' AND s."Metadata"->>'survivorId' = {survivor}
                       AND c."TimelineKey" = {f.TimelineKey}))
              AND ({f.SpoilerSeriesMax}::int IS NULL OR c."SpoilerSeriesMin" IS NULL OR c."SpoilerSeriesMin" <= {f.SpoilerSeriesMax})
              AND ({f.SurvivorDayMax}::int IS NULL OR c."SurvivorDayMin" IS NULL OR c."SurvivorDayMin" <= {f.SurvivorDayMax})
              AND ({types}::text[] IS NULL OR s."SourceType" = ANY({types}))
            ORDER BY "Score" DESC
            LIMIT {limit}
            """).ToArrayAsync(ct);
    }
}
