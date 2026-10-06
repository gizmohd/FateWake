namespace Fatewake.Infrastructure.Persistence;
/// <summary>Durably records an artwork provider invocation before execution and its resulting provider provenance afterward.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/ArtGenerationRecord.md">ArtGenerationRecord documentation</see>.</remarks>
public sealed class ArtGenerationRecord
{
 public Guid Id{get;set;}
 public Guid WorkJobId{get;set;}
 public Guid? ArtAssetId{get;set;}
 public required string IdempotencyKey{get;set;}
 public required string Operation{get;set;}
 public string? PromptKey{get;set;}
 public string? PromptTemplateVersion{get;set;}
 public required string ResolvedPrompt{get;set;}
 public string? NegativePrompt{get;set;}
 public required string Provider{get;set;}
 public required string Model{get;set;}
 public string? ProviderJobId{get;set;}
 public string? PromptBuilderVersion{get;set;}
 public string? StyleBibleVersion{get;set;}
 public string ReferenceAssets{get;set;}="[]";
 public string GenerationParameters{get;set;}="{}";
 public string ContinuityVersions{get;set;}="{}";
 public required string VisualFingerprint{get;set;}
 public decimal? CostUsd{get;set;}
 public DateTimeOffset CreatedAt{get;set;}
 public DateTimeOffset? CompletedAt{get;set;}
}