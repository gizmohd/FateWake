namespace Fatewake.Infrastructure.Art;
/// <summary>Records whether an exact approved artwork asset already satisfies a work request.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Art/ArtResolveResult.md">ArtResolveResult documentation</see>.</remarks>
public sealed record ArtResolveResult(bool Reused,Guid? AssetId,string? MasterPngStorageKey);