using Microsoft.EntityFrameworkCore;

namespace Fatewake.Infrastructure.Persistence;

/// <summary>Persists RAG staging documents in PostgreSQL and removes them after successful Worker ingestion.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/RagStagingStore.md">RagStagingStore documentation</see>.</remarks>
public sealed class RagStagingStore(FatewakeDbContext db) : IRagStagingStore
{
    public async Task<Guid> PutAsync(string sourceKey, string contentHash, string fileName,
        string contentType, byte[] bytes, CancellationToken ct = default)
    {
        var rows = await db.Database.SqlQuery<Guid>($"""
            INSERT INTO rag_staging ("Id", "ContentHash", "FileName", "ContentType", "Bytes", "ByteSize", "CreatedAt", "SourceKey")
            VALUES ({Guid.NewGuid()}, {contentHash}, {fileName}, {contentType}, {bytes}, {bytes.LongLength}, now(), {sourceKey})
            ON CONFLICT ("ContentHash", "SourceKey") WHERE "SourceKey" <> ''
                DO UPDATE SET "ContentHash" = excluded."ContentHash"
            RETURNING "Id" AS "Value"
            """).ToArrayAsync(ct);
        return rows.Single();
    }

    public Task<RagStagingRecord?> GetAsync(Guid id, CancellationToken ct = default)
    {
        return db.RagStaging.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default) =>
        db.RagStaging.Where(row => row.Id == id && row.SourceKey != "").ExecuteDeleteAsync(ct);
}
