using Fatewake.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text;

namespace Fatewake.Infrastructure.Rag;

/// <summary>Stores provider vectors in the pgvector column; reserves unembedded chunks across workers.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Rag/RagEmbeddingService.md">Documentation</see>.</remarks>
public sealed class RagEmbeddingService(FatewakeDbContext db, IEmbeddingProvider provider,
    EmbeddingOptions options, ILogger<RagEmbeddingService> log) : IRagEmbeddingService
{
    /// <inheritdoc />
    public async Task<int> EmbedPendingAsync(CancellationToken ct)
    {
        if (!options.Enabled || string.IsNullOrWhiteSpace(options.ApiKey))
            return 0;
        var leaseId = Guid.NewGuid();
        var leaseSeconds = Math.Max(1, options.TimeoutSeconds) + 120L;
        var batch = await db.Database.SqlQuery<ReservedChunk>($"""
            WITH next AS (
                SELECT "Id"
                FROM rag_chunk
                WHERE "EmbeddedAt" IS NULL AND ("EmbeddingLeaseUntil" IS NULL OR "EmbeddingLeaseUntil" <= now())
                ORDER BY "CreatedAt", "Id"
                LIMIT {Math.Max(1, options.BatchSize)}
                FOR UPDATE SKIP LOCKED
            )
            UPDATE rag_chunk chunk
            SET "EmbeddingLeaseId" = {leaseId}, "EmbeddingLeaseUntil" = now() + {leaseSeconds} * interval '1 second'
            FROM next WHERE chunk."Id" = next."Id"
            RETURNING chunk."Id", chunk."Text"
            """).ToArrayAsync(ct);
        if (batch.Length == 0)
            return 0;

        var vectors = await provider.EmbedAsync(batch.Select(x => x.Text).ToArray(), null, null, ct);
        if (vectors.Count != batch.Length || vectors.Any(vector => vector.Length != provider.Dimensions))
            throw new InvalidDataException("The provider did not return one vector of the configured size per chunk.");

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var now = DateTimeOffset.UtcNow;
        var embedded = 0;
        for (var i = 0; i < batch.Length; i++)
        {
            var literal = FormatVector(vectors[i]);
            var id = batch[i].Id;
            embedded += await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE rag_chunk
                SET "Embedding" = {literal}::vector, "EmbeddingModel" = {provider.Model},
                    "EmbeddingDimensions" = {provider.Dimensions}, "EmbeddedAt" = {now},
                    "EmbeddingLeaseId" = NULL, "EmbeddingLeaseUntil" = NULL
                WHERE "Id" = {id} AND "EmbeddedAt" IS NULL AND "EmbeddingLeaseId" = {leaseId}
                """, ct);
        }
        await transaction.CommitAsync(ct);
        log.LogInformation("Embedded {ChunkCount} RAG chunks", embedded);
        return embedded;
    }

    private sealed record ReservedChunk(Guid Id, string Text);

    /// <summary>Formats a vector as a pgvector text literal such as <c>[0.1,0.2]</c>.</summary>
    public static string FormatVector(IReadOnlyList<float> vector)
    {
        var text = new StringBuilder("[");
        for (var i = 0; i < vector.Count; i++)
        {
            if (i > 0)
                text.Append(',');
            text.Append(vector[i].ToString("R", CultureInfo.InvariantCulture));
        }
        return text.Append(']').ToString();
    }
}
