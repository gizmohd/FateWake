using Fatewake.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text;

namespace Fatewake.Infrastructure.Rag;

/// <summary>Stores provider vectors in the pgvector column; idempotent because only unembedded chunks are selected.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Rag/RagEmbeddingService.md">Documentation</see>.</remarks>
public sealed class RagEmbeddingService(FatewakeDbContext db, IEmbeddingProvider provider,
    EmbeddingOptions options, ILogger<RagEmbeddingService> log) : IRagEmbeddingService
{
    /// <inheritdoc />
    public async Task<int> EmbedPendingAsync(CancellationToken ct)
    {
        if (!options.Enabled || string.IsNullOrWhiteSpace(options.ApiKey))
            return 0;
        var batch = await db.RagChunks.AsNoTracking()
            .Where(x => x.EmbeddedAt == null)
            .OrderBy(x => x.CreatedAt)
            .Take(Math.Max(1, options.BatchSize))
            .Select(x => new { x.Id, x.Text })
            .ToArrayAsync(ct);
        if (batch.Length == 0)
            return 0;
        var vectors = await provider.EmbedAsync(batch.Select(x => x.Text).ToArray(), null, null, ct);
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < batch.Length; i++)
        {
            var literal = FormatVector(vectors[i]);
            var id = batch[i].Id;
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE rag_chunk
                SET "Embedding" = {literal}::vector, "EmbeddingModel" = {provider.Model},
                    "EmbeddingDimensions" = {provider.Dimensions}, "EmbeddedAt" = {now}
                WHERE "Id" = {id}
                """, ct);
        }
        log.LogInformation("Embedded {ChunkCount} RAG chunks", batch.Length);
        return batch.Length;
    }

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
