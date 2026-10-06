namespace Fatewake.Infrastructure.Art;
/// <summary>Contains a generated master PNG and provider provenance returned by an art generator.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Art/GeneratedArtMaster.md">GeneratedArtMaster documentation</see>.</remarks>
public sealed record GeneratedArtMaster(byte[] MasterPng,string? ProviderJobId,decimal? CostUsd);