using System.Text.Json;
using Fatewake.Infrastructure.Persistence;
using Fatewake.Infrastructure.Work;
using Microsoft.EntityFrameworkCore;
namespace Fatewake.Infrastructure.Art;
/// <summary>Finalizes validated artwork and atomically activates survivors still waiting for the exact request.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Art/ArtFinalizeWorkStepHandler.md">ArtFinalizeWorkStepHandler documentation</see>.</remarks>
public sealed class ArtFinalizeWorkStepHandler(FatewakeDbContext db,IWorkArtifactStore artifacts):IWorkStepHandler
{
 public string StepType=>ArtWorkStepTypes.Finalize;
 public async Task<string?> ExecuteAsync(WorkStepExecutionContext context,CancellationToken ct)
 {
  var prior=await artifacts.GetAsync(context.JobId,"art.finalize",ct);if(prior is not null)return prior;
  var request=JsonSerializer.Deserialize<ArtWorkRequest>(context.Input)??throw new InvalidOperationException("Invalid art request.");
  var validation=JsonSerializer.Deserialize<ArtValidationArtifact>(await Required("art.validation"))!;
  if(!validation.Valid)throw new InvalidOperationException(validation.Error??"Artwork validation failed.");
  var master=JsonSerializer.Deserialize<ArtMasterArtifact>(await Required("art.master"))!;
  var metadata=JsonSerializer.Deserialize<ArtMetadataArtifact>(await Required("art.metadata"))!;
  var webp=JsonSerializer.Deserialize<ArtDerivativeArtifact>(await Required("art.webp"))!;
  var png=JsonSerializer.Deserialize<ArtDerivativeArtifact>(await Required("art.png"))!;
  await using var tx=await db.Database.BeginTransactionAsync(ct);
  var existing=await db.ArtAssets.Where(x=>x.VisualFingerprint==request.VisualFingerprint&&x.Status==ArtAssetStatus.Approved).OrderByDescending(x=>x.Version).FirstOrDefaultAsync(ct);
  var assetId=existing?.Id??Guid.NewGuid();var reused=existing is not null;var now=DateTimeOffset.UtcNow;
  if(existing is null)
  {
   var version=(await db.ArtAssets.Where(x=>x.Key==request.AssetKey).MaxAsync(x=>(int?)x.Version,ct)??0)+1;
   db.ArtAssets.Add(new ArtAssetRecord{Id=assetId,Key=request.AssetKey,Version=version,AssetType=request.AssetType,Status=ArtAssetStatus.Approved,CharacterKey=request.CharacterKey,LocationKey=request.LocationKey,VisualFingerprint=request.VisualFingerprint,ContentHash=metadata.ContentHash,MasterPngStorageKey=master.StorageKey,Width=metadata.Width,Height=metadata.Height,HasAlpha=metadata.HasAlpha,CreatedAt=now,ApprovedAt=now});
   db.ArtAssetDerivatives.Add(new ArtAssetDerivativeRecord{Id=Guid.NewGuid(),ArtAssetId=assetId,Kind=ArtDerivativeKind.WebP,StorageKey=webp.StorageKey,ContentType=webp.ContentType,ContentHash=webp.ContentHash,Width=webp.Width,Height=webp.Height,ByteSize=webp.ByteSize,EncoderMetadata=webp.EncoderMetadata,CreatedAt=now});
   db.ArtAssetDerivatives.Add(new ArtAssetDerivativeRecord{Id=Guid.NewGuid(),ArtAssetId=assetId,Kind=ArtDerivativeKind.OptimizedPng,StorageKey=png.StorageKey,ContentType=png.ContentType,ContentHash=png.ContentHash,Width=png.Width,Height=png.Height,ByteSize=png.ByteSize,EncoderMetadata=png.EncoderMetadata,CreatedAt=now});
  }
  var waiting=await db.SurvivorVisualIdentities.Where(x=>x.RequestedWorkJobId==context.JobId&&x.RequestedVisualFingerprint==request.VisualFingerprint&&x.Status==SurvivorVisualIdentityStatus.GenerationPending).ToListAsync(ct);
  foreach(var identity in waiting){identity.ActiveArtAssetId=assetId;identity.Status=reused?SurvivorVisualIdentityStatus.Reused:SurvivorVisualIdentityStatus.Generated;identity.RequestedWorkJobId=null;identity.RequestedVisualFingerprint=null;identity.ActivatedAt=now;identity.UpdatedAt=now;}
  await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
  var result=JsonSerializer.Serialize(new ArtFinalizeResult(assetId,reused,waiting.Count));await artifacts.PutAsync(context.JobId,"art.finalize",result,ct);return result;
  async Task<string> Required(string key)=>await artifacts.GetAsync(context.JobId,key,ct)??throw new InvalidOperationException($"Required artifact {key} is missing.");
 }
}