namespace Fatewake.Infrastructure.Persistence;

/// <summary>Looks up reusable approved artwork by canonical fingerprint or explicit key/version.</summary>
/// <see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/IArtAssetStore.md">IArtAssetStore documentation</see>
public interface IArtAssetStore
{
    /// <summary>Finds the latest approved artwork matching the exact visual fingerprint.</summary>
    /// <param name="visualFingerprint">Canonical normalized visual fingerprint.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The approved asset and its derivatives, or null when none exists.</returns>
    Task<ArtAssetMatch?> FindApprovedByFingerprintAsync(string visualFingerprint, CancellationToken ct = default);

    /// <summary>Finds an approved artwork version by its explicit logical key.</summary>
    /// <param name="key">Logical asset key.</param>
    /// <param name="version">Required immutable version.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The approved asset and its derivatives, or null when none exists.</returns>
    Task<ArtAssetMatch?> FindApprovedAsync(string key, int version, CancellationToken ct = default);
}
