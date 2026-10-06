namespace Fatewake.Infrastructure.Art;
/// <summary>Identifies the durable master PNG selected or generated for an artwork job.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Art/ArtMasterArtifact.md">ArtMasterArtifact documentation</see>.</remarks>
public sealed record ArtMasterArtifact(string StorageKey,bool Reused,Guid? ExistingAssetId,string Provider,string Model,string? ProviderJobId,decimal? CostUsd);