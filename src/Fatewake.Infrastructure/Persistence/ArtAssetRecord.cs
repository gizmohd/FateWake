namespace Fatewake.Infrastructure.Persistence;

/// <summary>Stores an immutable version of approved artwork and its reusable ownership identity.</summary>
/// <see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/ArtAssetRecord.md">ArtAssetRecord documentation</see>
public sealed class ArtAssetRecord
{
    /// <summary>Stable database identifier for this immutable asset version.</summary>
    public Guid Id { get; set; }

    /// <summary>Logical artwork key used by authored scenes and versioned delivery URLs.</summary>
    public required string Key { get; set; }

    /// <summary>Immutable version number within the logical key.</summary>
    public int Version { get; set; } = 1;

    /// <summary>Authored role, such as a background, prop, or character pose.</summary>
    public required string AssetType { get; set; }

    /// <summary>Review lifecycle state that gates runtime delivery.</summary>
    public ArtAssetStatus Status { get; set; } = ArtAssetStatus.Draft;

    /// <summary>Optional source asset for an edited or derived version.</summary>
    public Guid? ParentAssetId { get; set; }

    /// <summary>Optional canonical character identity represented by the asset.</summary>
    public string? CharacterKey { get; set; }

    /// <summary>Optional canonical location identity represented by the asset.</summary>
    public string? LocationKey { get; set; }

    /// <summary>Owning account for non-shareable survivor artwork; null denotes approved shared art.</summary>
    public Guid? OwnerAccountId { get; set; }

    /// <summary>Canonical normalized visual identity used for exact reuse.</summary>
    public required string VisualFingerprint { get; set; }

    /// <summary>Content hash of the archival PNG master.</summary>
    public required string ContentHash { get; set; }

    /// <summary>Stable storage key for the archival PNG master.</summary>
    public required string MasterPngStorageKey { get; set; }

    /// <summary>Pixel width of the archival master.</summary>
    public int Width { get; set; }

    /// <summary>Pixel height of the archival master.</summary>
    public int Height { get; set; }

    /// <summary>Whether the master includes transparent pixels.</summary>
    public bool HasAlpha { get; set; }

    /// <summary>UTC time when this asset version was registered.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>UTC time when review approved this version.</summary>
    public DateTimeOffset? ApprovedAt { get; set; }
}
