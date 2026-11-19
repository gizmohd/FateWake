namespace Fatewake.Api;

/// <summary>Contains validated server-derived identity and framing for one allowlisted scene layer.</summary>
/// <see href="../../../docs/code/src/Fatewake.Api/SceneArtPromptMetadata.md">Documentation</see>
/// <param name="AssetKey">Allowlisted scene-layer key.</param>
/// <param name="AssetType">Server-derived layer role.</param>
/// <param name="IdentityKey">SQL prompt identity including canonical context and style scope.</param>
/// <param name="ContextFingerprint">Canonical visual context fingerprint.</param>
/// <param name="StyleBibleVersion">Shared visual style version.</param>
/// <param name="CanonicalContext">Safe visual continuity facts for the layer.</param>
/// <param name="Framing">Enforced alpha and geometry requirements.</param>
/// <param name="ReachedVisualContext">Allowlisted reached outcome that scopes outcome-dependent scene art.</param>
internal sealed record SceneArtPromptMetadata(string AssetKey, string AssetType, string IdentityKey,
    string ContextFingerprint, string StyleBibleVersion, string CanonicalContext, string Framing,
    string? ReachedVisualContext);
