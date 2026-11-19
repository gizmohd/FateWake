using Microsoft.EntityFrameworkCore;

namespace Fatewake.Infrastructure.Persistence;

/// <summary>Implements durable, idempotent artwork regeneration queue persistence.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/ArtRegenerationStore.md">ArtRegenerationStore documentation</see>.</remarks>
public sealed class ArtRegenerationStore(FatewakeDbContext db) : IArtRegenerationStore
{
    public async Task<ArtRegenerationRecord> QueueAsync(Guid sourceWorkJobId, string visualFingerprint, string requestJson, string targetProvider, string? targetModel, string reason, CancellationToken ct = default)
    {
        var existing = await db.ArtRegenerationRequests.SingleOrDefaultAsync(x => x.SourceWorkJobId == sourceWorkJobId && x.Status != "Completed", ct);
        if (existing is not null) return existing;
        var now = DateTimeOffset.UtcNow;
        var record = new ArtRegenerationRecord
        {
            Id = Guid.NewGuid(),
            SourceWorkJobId = sourceWorkJobId,
            VisualFingerprint = visualFingerprint,
            RequestJson = requestJson,
            TargetProvider = targetProvider,
            TargetModel = targetModel,
            Status = "Pending",
            Reason = reason,
            AttemptCount = 0,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.ArtRegenerationRequests.Add(record);
        await db.SaveChangesAsync(ct);
        return record;
    }
}
