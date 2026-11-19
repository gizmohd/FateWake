using Microsoft.EntityFrameworkCore;

namespace Fatewake.Infrastructure.Persistence;

/// <summary>Persists RAG staging documents in PostgreSQL and removes them after successful Worker ingestion.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/RagStagingStore.md">RagStagingStore documentation</see>.</remarks>
public sealed class RagStagingStore(FatewakeDbContext db) : IRagStagingStore
{
    public async Task<RagStagingRecord> PutAsync(string contentHash, string fileName, string contentType, byte[] bytes, CancellationToken ct = default)
    {
        var existing = await db.RagStaging
            .SingleOrDefaultAsync(x => x.ContentHash == contentHash && x.FileName == fileName, ct);
        var now = DateTimeOffset.UtcNow;
        if (existing is not null)
        {
            existing.ContentType = contentType;
            existing.Bytes = bytes;
            existing.ByteSize = bytes.LongLength;
            await db.SaveChangesAsync(ct);
            return existing;
        }
        var record = new RagStagingRecord
        {
            Id = Guid.NewGuid(),
            ContentHash = contentHash,
            FileName = fileName,
            ContentType = contentType,
            Bytes = bytes,
            ByteSize = bytes.LongLength,
            CreatedAt = now
        };
        db.RagStaging.Add(record);
        await db.SaveChangesAsync(ct);
        return record;
    }

    public Task<RagStagingRecord?> GetAsync(Guid id, CancellationToken ct = default)
    {
        return db.RagStaging.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var record = await db.RagStaging.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (record is null)
            return;
        db.RagStaging.Remove(record);
        await db.SaveChangesAsync(ct);
    }
}
