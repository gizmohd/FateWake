namespace Fatewake.Infrastructure.Persistence;
/// <summary>Persists artwork provider invocation provenance independently of worker lifetime.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/IArtGenerationStore.md">IArtGenerationStore documentation</see>.</remarks>
public interface IArtGenerationStore
{
 Task<ArtGenerationRecord> EnsurePendingAsync(Guid workJobId,string idempotencyKey,string operation,string resolvedPrompt,string? negativePrompt,string provider,string model,string? promptKey,string? promptTemplateVersion,string? styleBibleVersion,string referenceAssets,string generationParameters,string continuityVersions,string visualFingerprint,CancellationToken ct=default);
 Task CompleteAsync(Guid workJobId,string? providerJobId,decimal? costUsd,CancellationToken ct=default);
}