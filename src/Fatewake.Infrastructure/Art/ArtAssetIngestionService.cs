using System.Security.Cryptography;
using System.Text.Json;
using Fatewake.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fatewake.Infrastructure.Art;

public sealed record ArtGenerationProvenance(
    string Operation,string? PromptKey,string? PromptTemplateVersion,string ResolvedPrompt,string? NegativePrompt,
    string Provider,string Model,string? ProviderJobId,string? PromptBuilderVersion,string? StyleBibleVersion,
    IReadOnlyList<string> ReferenceAssets,IReadOnlyDictionary<string,object?> GenerationParameters,
    IReadOnlyDictionary<string,string> ContinuityVersions,decimal? CostUsd);

public sealed record ArtIngestRequest(
    string Key,string AssetType,string VisualFingerprint,byte[] MasterPng,ArtGenerationProvenance Provenance,
    string? CharacterKey=null,string? LocationKey=null,Guid? ParentAssetId=null,bool Approve=false);

public sealed record ArtIngestResult(Guid AssetId,string Key,int Version,string MasterPngStorageKey,string WebPStorageKey,string OptimizedPngStorageKey);

public interface IArtAssetIngestionService
{
    Task<ArtIngestResult> IngestAsync(ArtIngestRequest request,CancellationToken ct=default);
}

public sealed class ArtAssetIngestionService(FatewakeDbContext db,IArtBinaryStorage storage,IArtImageProcessor images):IArtAssetIngestionService
{
    public async Task<ArtIngestResult> IngestAsync(ArtIngestRequest request,CancellationToken ct=default)
    {
        var existing=await db.ArtAssets.AsNoTracking().Where(x=>x.VisualFingerprint==request.VisualFingerprint&&x.Status==ArtAssetStatus.Approved)
            .OrderByDescending(x=>x.Version).FirstOrDefaultAsync(ct);
        if(existing is not null)
        {
            var derivatives=await db.ArtAssetDerivatives.AsNoTracking().Where(x=>x.ArtAssetId==existing.Id).ToListAsync(ct);
            return new(existing.Id,existing.Key,existing.Version,existing.MasterPngStorageKey,
                derivatives.Single(x=>x.Kind==ArtDerivativeKind.WebP).StorageKey,
                derivatives.Single(x=>x.Kind==ArtDerivativeKind.OptimizedPng).StorageKey);
        }

        var info=images.InspectPng(request.MasterPng);
        var webp=await images.CreateWebPAsync(request.MasterPng,ct);
        var optimizedPng=await images.CreateOptimizedPngAsync(request.MasterPng,ct);
        var version=(await db.ArtAssets.Where(x=>x.Key==request.Key).MaxAsync(x=>(int?)x.Version,ct)??0)+1;
        var id=Guid.NewGuid();var now=DateTimeOffset.UtcNow;
        var prefix=$"art/{request.Key}/v{version}";
        var masterKey=$"{prefix}/master.png";var webpKey=$"{prefix}/delivery.webp";var pngKey=$"{prefix}/delivery.png";

        await PutAsync(masterKey,request.MasterPng,"image/png",ct);
        await PutAsync(webpKey,webp.Bytes,webp.ContentType,ct);
        await PutAsync(pngKey,optimizedPng.Bytes,optimizedPng.ContentType,ct);

        var asset=new ArtAssetRecord{Id=id,Key=request.Key,Version=version,AssetType=request.AssetType,
            Status=request.Approve?ArtAssetStatus.Approved:ArtAssetStatus.Draft,ParentAssetId=request.ParentAssetId,
            CharacterKey=request.CharacterKey,LocationKey=request.LocationKey,VisualFingerprint=request.VisualFingerprint,
            ContentHash=Hash(request.MasterPng),MasterPngStorageKey=masterKey,Width=info.Width,Height=info.Height,HasAlpha=info.HasAlpha,
            CreatedAt=now,ApprovedAt=request.Approve?now:null};
        db.ArtAssets.Add(asset);
        db.ArtAssetDerivatives.AddRange(
            Derivative(id,ArtDerivativeKind.WebP,webpKey,webp,now),
            Derivative(id,ArtDerivativeKind.OptimizedPng,pngKey,optimizedPng,now));
        db.ArtGenerations.Add(new ArtGenerationRecord{Id=Guid.NewGuid(),IdempotencyKey=id.ToString("N"),ArtAssetId=id,Operation=request.Provenance.Operation,
            PromptKey=request.Provenance.PromptKey,PromptTemplateVersion=request.Provenance.PromptTemplateVersion,
            ResolvedPrompt=request.Provenance.ResolvedPrompt,NegativePrompt=request.Provenance.NegativePrompt,
            Provider=request.Provenance.Provider,Model=request.Provenance.Model,ProviderJobId=request.Provenance.ProviderJobId,
            PromptBuilderVersion=request.Provenance.PromptBuilderVersion,StyleBibleVersion=request.Provenance.StyleBibleVersion,
            ReferenceAssets=JsonSerializer.Serialize(request.Provenance.ReferenceAssets),
            GenerationParameters=JsonSerializer.Serialize(request.Provenance.GenerationParameters),
            ContinuityVersions=JsonSerializer.Serialize(request.Provenance.ContinuityVersions),
            VisualFingerprint=request.VisualFingerprint,CostUsd=request.Provenance.CostUsd,CreatedAt=now});
        await db.SaveChangesAsync(ct);
        return new(id,request.Key,version,masterKey,webpKey,pngKey);
    }

    private async Task PutAsync(string key,byte[] bytes,string contentType,CancellationToken ct)
    { await using var stream=new MemoryStream(bytes,writable:false);await storage.PutAsync(key,stream,contentType,ct); }
    private static ArtAssetDerivativeRecord Derivative(Guid id,ArtDerivativeKind kind,string key,EncodedArtImage image,DateTimeOffset now)
        =>new(){Id=Guid.NewGuid(),ArtAssetId=id,Kind=kind,StorageKey=key,ContentType=image.ContentType,ContentHash=Hash(image.Bytes),
            Width=image.Info.Width,Height=image.Info.Height,ByteSize=image.Bytes.LongLength,EncoderMetadata=image.EncoderMetadata,CreatedAt=now};
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
