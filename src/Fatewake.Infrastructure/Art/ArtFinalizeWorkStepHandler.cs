using System.Text.Json;
using Fatewake.Infrastructure.Persistence;
using Fatewake.Infrastructure.Work;
using Microsoft.EntityFrameworkCore;
namespace Fatewake.Infrastructure.Art;
/// <summary>Finalizes validated artwork and atomically activates survivors still waiting for the exact request.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Art/ArtFinalizeWorkStepHandler.md">ArtFinalizeWorkStepHandler documentation</see>.</remarks>
public sealed class ArtFinalizeWorkStepHandler(FatewakeDbContext db, IWorkArtifactStore artifacts) : IWorkStepHandler
{
    public string StepType => ArtWorkStepTypes.Finalize;
    public async Task<string?> ExecuteAsync(WorkStepExecutionContext context, CancellationToken ct)
    {
        var prior = await artifacts.GetAsync(context.JobId, "art.finalize", ct);
        if (prior is not null)
            return prior;
        var request = JsonSerializer.Deserialize<ArtWorkRequest>(context.Input) ?? throw new InvalidOperationException("Invalid art request.");
        if (request.IsPrivate && request.AccountId is null)
            throw new InvalidOperationException("Private artwork requires an owning account.");
        var validation = JsonSerializer.Deserialize<ArtValidationArtifact>(await Required("art.validation"))!;
        if (!validation.Valid)
            throw new InvalidOperationException(validation.Error ?? "Artwork validation failed.");
        var master = JsonSerializer.Deserialize<ArtMasterArtifact>(await Required("art.master"))!;
        var metadata = JsonSerializer.Deserialize<ArtMetadataArtifact>(await Required("art.metadata"))!;
        var webp = JsonSerializer.Deserialize<ArtDerivativeArtifact>(await Required("art.webp"))!;
        var png = JsonSerializer.Deserialize<ArtDerivativeArtifact>(await Required("art.png"))!;
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var existing = request.ForceProviderRegeneration ? null : await db.ArtAssets.Where(x => x.VisualFingerprint == request.VisualFingerprint && x.Status == ArtAssetStatus.Approved &&
         (x.OwnerAccountId == null || request.IsPrivate && x.OwnerAccountId == request.AccountId))
         .OrderBy(x => x.OwnerAccountId == request.AccountId ? 0 : 1).ThenByDescending(x => x.Version).FirstOrDefaultAsync(ct);
        var assetId = existing?.Id ?? Guid.NewGuid();
        var reused = existing is not null;
        var now = DateTimeOffset.UtcNow;
        if (existing is null)
        {
            var version = (await db.ArtAssets.Where(x => x.Key == request.AssetKey).MaxAsync(x => (int?)x.Version, ct) ?? 0) + 1;
            db.ArtAssets.Add(new ArtAssetRecord { Id = assetId, Key = request.AssetKey, Version = version, AssetType = request.AssetType, Status = ArtAssetStatus.Approved, CharacterKey = request.CharacterKey, LocationKey = request.LocationKey, OwnerAccountId = request.IsPrivate ? request.AccountId : null, VisualFingerprint = request.VisualFingerprint, ContentHash = metadata.ContentHash, MasterPngStorageKey = master.StorageKey, Width = metadata.Width, Height = metadata.Height, HasAlpha = metadata.HasAlpha, CreatedAt = now, ApprovedAt = now });
            db.ArtAssetDerivatives.Add(new ArtAssetDerivativeRecord { Id = Guid.NewGuid(), ArtAssetId = assetId, Kind = ArtDerivativeKind.WebP, StorageKey = webp.StorageKey, ContentType = webp.ContentType, ContentHash = webp.ContentHash, Width = webp.Width, Height = webp.Height, ByteSize = webp.ByteSize, EncoderMetadata = webp.EncoderMetadata, CreatedAt = now });
            db.ArtAssetDerivatives.Add(new ArtAssetDerivativeRecord { Id = Guid.NewGuid(), ArtAssetId = assetId, Kind = ArtDerivativeKind.OptimizedPng, StorageKey = png.StorageKey, ContentType = png.ContentType, ContentHash = png.ContentHash, Width = png.Width, Height = png.Height, ByteSize = png.ByteSize, EncoderMetadata = png.EncoderMetadata, CreatedAt = now });
        }
        var waiting = await db.SurvivorVisualIdentities.Where(x => x.RequestedWorkJobId == context.JobId && x.RequestedVisualFingerprint == request.VisualFingerprint && x.Status == SurvivorVisualIdentityStatus.GenerationPending).ToListAsync(ct);
        foreach (var identity in waiting)
        {
            identity.ActiveArtAssetId = assetId;
            identity.Status = reused ? SurvivorVisualIdentityStatus.Reused : SurvivorVisualIdentityStatus.Generated;
            identity.RequestedWorkJobId = null;
            identity.RequestedVisualFingerprint = null;
            identity.ActivatedAt = now;
            identity.UpdatedAt = now;
        }
        var generation = await db.ArtGenerations.SingleOrDefaultAsync(row => row.WorkJobId == context.JobId, ct);
        if (generation is not null)
            generation.ArtAssetId = assetId;
        if (request.PromptRevisionId is { } revisionId)
        {
            var prompt = await db.Set<ArtPromptRecord>().SingleOrDefaultAsync(row =>
                row.RequestedRevisionId == revisionId && row.RequestedWorkJobId == context.JobId, ct);
            if (prompt is not null)
            {
                prompt.CurrentRevisionId = revisionId;
                prompt.CurrentRevisionNumber = prompt.RequestedRevisionNumber;
                prompt.RequestedRevisionId = null;
                prompt.RequestedRevisionNumber = null;
                prompt.RequestedAssetId = null;
                prompt.RequestedWorkJobId = null;
                prompt.UpdatedAt = now;
            }
        }
        var regeneration = await db.ArtRegenerationRequests
            .SingleOrDefaultAsync(row => row.RegenerationWorkJobId == context.JobId, ct);
        if (regeneration is not null)
        {
            regeneration.Status = "Completed";
            regeneration.UpdatedAt = now;
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        var result = JsonSerializer.Serialize(new ArtFinalizeResult(assetId, reused, waiting.Count));
        await artifacts.PutAsync(context.JobId, "art.finalize", result, ct);
        return result;
        async Task<string> Required(string key) => await artifacts.GetAsync(context.JobId, key, ct) ?? throw new InvalidOperationException($"Required artifact {key} is missing.");
    }
}