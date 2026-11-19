using System.Text.Json;
using Fatewake.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fatewake.Api;

/// <summary>Imports the versioned prompt seed once without replacing any database-authored state.</summary>
/// <see href="../../../docs/code/src/Fatewake.Api/ArtPromptSeedImporter.md">ArtPromptSeedImporter documentation</see>
public sealed class ArtPromptSeedImporter(FatewakeDbContext db, SceneArtCatalog catalog,
    ILogger<ArtPromptSeedImporter> log)
{
    /// <summary>Creates missing seed prompt identities and their first immutable revisions in one transaction.</summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "SceneArtSeeds", "day1-v1.json");
        if (!File.Exists(path))
            throw new FileNotFoundException("The versioned artwork prompt seed is missing.", path);
        await using var stream = File.OpenRead(path);
        var seed = await JsonSerializer.DeserializeAsync<SeedDocument>(stream,
            new JsonSerializerOptions(JsonSerializerDefaults.Web), ct)
            ?? throw new InvalidDataException("The artwork prompt seed is empty.");
        if (seed.SeedVersion != "day1-authored-v1" || seed.Prompts.Count == 0)
            throw new InvalidDataException("The artwork prompt seed version or contents are invalid.");

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var advisoryKey = "art-prompt-seed:" + seed.SeedVersion;
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({advisoryKey}, 0))", ct);
        var imported = 0;
        var seeded = new List<(ArtPromptRecord Prompt, ArtPromptRevisionRecord Revision)>();
        foreach (var entry in seed.Prompts)
        {
            if (!catalog.Contains(entry.AssetKey) || string.IsNullOrWhiteSpace(entry.PositivePrompt) ||
                entry.PositivePrompt.Length > 8000 || entry.NegativePrompt?.Length > 2000)
                throw new InvalidDataException("The artwork prompt seed contains invalid scene metadata or prompt content.");
            var metadata = catalog.Describe(entry.AssetKey);
            if (await db.Set<ArtPromptRecord>().AnyAsync(row => row.IdentityKey == metadata.IdentityKey, ct))
                continue;

            var revision = new ArtPromptRevisionRecord
            {
                Id = Guid.NewGuid(),
                RevisionNumber = 1,
                PositivePrompt = entry.PositivePrompt.Trim(),
                NegativePrompt = string.IsNullOrWhiteSpace(entry.NegativePrompt) ? null : entry.NegativePrompt.Trim(),
                Source = "seed",
                CreatedAt = DateTimeOffset.UtcNow
            };
            var prompt = new ArtPromptRecord
            {
                Id = Guid.NewGuid(),
                IdentityKey = metadata.IdentityKey,
                AssetKey = metadata.AssetKey,
                ContextFingerprint = metadata.ContextFingerprint,
                StyleBibleVersion = metadata.StyleBibleVersion,
                ReachedVisualContext = metadata.ReachedVisualContext,
                Status = "ready",
                LatestRevisionNumber = 1,
                CurrentRevisionNumber = 1,
                CreatedAt = revision.CreatedAt,
                UpdatedAt = revision.CreatedAt
            };
            revision.ArtPromptId = prompt.Id;
            var legacyFingerprint = catalog.ComputeLegacyFingerprint(metadata, revision);
            var equivalentGenerations = await db.Set<ArtGenerationRecord>().Where(row =>
                    row.PromptKey == entry.AssetKey && row.PromptTemplateVersion == "4" &&
                    row.StyleBibleVersion == metadata.StyleBibleVersion && row.VisualFingerprint == legacyFingerprint &&
                    row.PromptRevisionId == null)
                .ToArrayAsync(ct);
            foreach (var generation in equivalentGenerations)
                generation.PromptRevisionId = revision.Id;
            db.Add(prompt);
            db.Add(revision);
            seeded.Add((prompt, revision));
            imported++;
        }

        // The prompt and its first revision reference each other, so the revision pointers are set after both rows exist.
        await db.SaveChangesAsync(ct);
        foreach (var (prompt, revision) in seeded)
        {
            prompt.LatestRevisionId = revision.Id;
            prompt.CurrentRevisionId = revision.Id;
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        log.LogInformation("Imported {PromptCount} missing SQL artwork prompt seeds", imported);
    }

    private sealed record SeedDocument(string SeedVersion, IReadOnlyList<SeedPrompt> Prompts);
    private sealed record SeedPrompt(string AssetKey, string PositivePrompt, string? NegativePrompt,
        string Source, string SourceFile);
}
