using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Fatewake.Infrastructure.Authentication;
using Fatewake.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fatewake.Infrastructure.Story;

/// <summary>Stores reviewed local continuity in the RAG corpus and reads it only for a later scene in the same Thread.</summary>
internal static class ThreadCanon
{
    internal const string SourceType = "GeneratedThreadCanon";

    // Runtime summary of docs/WORLD.md and the structural rules in the Season One world bible.
    internal const string WorldRules = """
        Season One, The Silence, begins in a recognizable near-future United States. Communications and electricity fail,
        emergency broadcasts conflict, and ordinary survivors face practical choices about trust, scarcity and mutual aid.
        Keep the opening local and NPC-driven. Let communities and contact with other Threads emerge only through established events.
        The cause of The Silence and the meaning of 6:17 remain unresolved. Treat anomalous broadcasts as observations, not explanations.
        Day 1 starts at 6:17 AM and powered devices fail together at 6:43 AM. Preserve the player's established opening outcome.
        Do not bring forward the unpowered radio event reserved for Day 4 or the shared signal choice reserved for Day 7.
        Keep the Echo and deeper Wayfinder connections hidden. Respect authored milestones and knowledge already earned.
        Expand only the current Thread with observable local details compatible with existing canon.
        Never invent shared history, change established characters or world rules, confirm a major mystery,
        or make the player's decision or its mechanical effects authoritative through narration.
        """;

    internal static Task<List<string>> ReadAsync(FatewakeDbContext db, GeneratedSceneRecord current,
        int day, ContentLevel level, int limit, CancellationToken ct)
    {
        var timelineKey = SceneWork.TimelineKey(current.TimelineId);
        return (from scene in db.Set<GeneratedSceneRecord>()
                join source in db.RagSources on scene.RagSourceId equals (Guid?)source.Id
                join document in db.RagDocuments on source.Id equals document.SourceId
                join chunk in db.RagChunks on document.Id equals chunk.DocumentId
                where scene.SurvivorId == current.SurvivorId && scene.TimelineId == current.TimelineId &&
                    scene.Sequence < current.Sequence && scene.Status == GeneratedSceneStatus.Ready &&
                    scene.ContentLevel != null && scene.ContentLevel <= level &&
                    source.SourceType == SourceType && source.Visibility == "Private" && source.ReviewStatus == "Approved" &&
                    document.Status == "Approved" && chunk.ReviewStatus == "Approved" &&
                    chunk.TimelineKey == timelineKey && chunk.SurvivorDayMin <= day && chunk.SpoilerSeriesMin <= 1
                orderby scene.Sequence descending
                select chunk.Text).Take(limit).ToListAsync(ct);
    }

    internal static void Add(FatewakeDbContext db, GeneratedSceneRecord scene, string summary)
    {
        var sourceId = scene.Id;
        var documentId = Guid.NewGuid();
        var sourceKey = $"thread-canon:{scene.Id:N}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(summary))).ToLowerInvariant();
        var now = scene.UpdatedAt;
        const string title = "Generated Thread continuity";

        db.RagSources.Add(new RagSourceRecord
        {
            Id = sourceId, SourceKey = sourceKey, SourceType = SourceType, Title = title,
            Origin = "GeneratedScene", ContentHash = hash, LicenseStatus = "Authorized",
            ReviewStatus = "Approved", Visibility = "Private",
            Metadata = JsonSerializer.Serialize(new { survivorId = scene.SurvivorId, sceneId = scene.Id }),
            CreatedAt = now, UpdatedAt = now
        });
        db.RagDocuments.Add(new RagDocumentRecord
        {
            Id = documentId, SourceId = sourceId, DocumentKey = "continuity", DocumentType = SourceType,
            Title = title, Sequence = scene.Sequence, ContentHash = hash, Status = "Approved",
            CreatedAt = now, UpdatedAt = now
        });
        db.RagChunks.Add(new RagChunkRecord
        {
            Id = Guid.NewGuid(), DocumentId = documentId, ChunkKey = sourceKey, Text = summary,
            TimelineKey = SceneWork.TimelineKey(scene.TimelineId), SurvivorDayMin = scene.SurvivorDay,
            SpoilerSeriesMin = 1, CanonStatus = "ThreadCanon", ReviewStatus = "Approved",
            CreatedAt = now, UpdatedAt = now
        });
        scene.RagSourceId = sourceId;
    }
}
