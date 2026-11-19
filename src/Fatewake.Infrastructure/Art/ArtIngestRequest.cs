namespace Fatewake.Infrastructure.Art;

/// <summary>Supplies a canonical archival master and generation history for asset registration.</summary>
/// <param name="Key">Logical artwork key.</param>
/// <param name="AssetType">Authored visual role.</param>
/// <param name="VisualFingerprint">Canonical normalized visual identity.</param>
/// <param name="MasterPng">Full-resolution archival PNG bytes.</param>
/// <param name="Provenance">Immutable generation/edit history.</param>
/// <param name="CharacterKey">Optional character identity represented by the asset.</param>
/// <param name="LocationKey">Optional location identity represented by the asset.</param>
/// <param name="ParentAssetId">Optional source asset for an edited derivative.</param>
/// <param name="Approve">Whether this version enters the approved reuse pool.</param>
/// <see href="../../../docs/code/src/Fatewake.Infrastructure/Art/ArtIngestRequest.md">ArtIngestRequest documentation</see>
public sealed record ArtIngestRequest(
    string Key,
    string AssetType,
    string VisualFingerprint,
    byte[] MasterPng,
    ArtGenerationProvenance Provenance,
    string? CharacterKey = null,
    string? LocationKey = null,
    Guid? ParentAssetId = null,
    bool Approve = false);
