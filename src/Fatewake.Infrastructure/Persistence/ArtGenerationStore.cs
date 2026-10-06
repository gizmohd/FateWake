using Microsoft.EntityFrameworkCore;
namespace Fatewake.Infrastructure.Persistence;
/// <summary>Implements idempotent PostgreSQL persistence for artwork provider invocation provenance.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/ArtGenerationStore.md">ArtGenerationStore documentation</see>.</remarks>
public sealed class ArtGenerationStore(FatewakeDbContext db):IArtGenerationStore
{
 public async Task<ArtGenerationRecord> EnsurePendingAsync(Guid workJobId,string idempotencyKey,string operation,string resolvedPrompt,string? negativePrompt,string provider,string model,string? promptKey,string? promptTemplateVersion,string? styleBibleVersion,string referenceAssets,string generationParameters,string continuityVersions,string visualFingerprint,CancellationToken ct=default)
 {
  var existing=await db.ArtGenerations.SingleOrDefaultAsync(x=>x.WorkJobId==workJobId,ct);if(existing is not null)return existing;
  var record=new ArtGenerationRecord{Id=Guid.NewGuid(),WorkJobId=workJobId,IdempotencyKey=idempotencyKey,Operation=operation,ResolvedPrompt=resolvedPrompt,NegativePrompt=negativePrompt,Provider=provider,Model=model,PromptKey=promptKey,PromptTemplateVersion=promptTemplateVersion,StyleBibleVersion=styleBibleVersion,ReferenceAssets=referenceAssets,GenerationParameters=generationParameters,ContinuityVersions=continuityVersions,VisualFingerprint=visualFingerprint,CreatedAt=DateTimeOffset.UtcNow};
  db.ArtGenerations.Add(record);
  try{await db.SaveChangesAsync(ct);return record;}catch(DbUpdateException){db.Entry(record).State=EntityState.Detached;return await db.ArtGenerations.SingleAsync(x=>x.WorkJobId==workJobId,ct);}
 }
 public async Task CompleteAsync(Guid workJobId,string? providerJobId,decimal? costUsd,CancellationToken ct=default)
 {
  var record=await db.ArtGenerations.SingleAsync(x=>x.WorkJobId==workJobId,ct);record.ProviderJobId=providerJobId;record.CostUsd=costUsd;record.CompletedAt=DateTimeOffset.UtcNow;await db.SaveChangesAsync(ct);
 }
}