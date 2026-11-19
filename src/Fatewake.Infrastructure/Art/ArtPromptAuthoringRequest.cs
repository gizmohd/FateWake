namespace Fatewake.Infrastructure.Art;

/// <summary>Captures safe, server-authored visual facts before delayed prompt-generation work is queued.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Art/ArtPromptAuthoringRequest.md">Documentation</see>.</remarks>
/// <param name="IdentityKey">Server-generated SQL identity used to coalesce this prompt family.</param>
/// <param name="AssetKey">Allowlisted scene-layer key.</param>
/// <param name="AssetType">Server-derived layer role.</param>
/// <param name="ContextFingerprint">Canonical visual context fingerprint.</param>
/// <param name="StyleBibleVersion">Shared style contract version captured at request time.</param>
/// <param name="CanonicalContext">Bounded canonical visual facts, never freeform player text.</param>
/// <param name="Framing">Server-owned geometry and alpha requirements.</param>
/// <param name="RuntimeStyle">Shared versioned positive style instructions.</param>
/// <param name="RuntimeExclusions">Shared negative style constraints.</param>
/// <param name="OwnerAccountId">Owner for safe usage accounting or private context.</param>
/// <param name="SurvivorId">Stable survivor identity for usage accounting; not sent to the provider.</param>
/// <param name="LegacyVisualFingerprint">Exact legacy cache candidate for a proven seed equivalence.</param>
public sealed record ArtPromptAuthoringRequest(
    string IdentityKey,
    string AssetKey,
    string AssetType,
    string ContextFingerprint,
    string StyleBibleVersion,
    string CanonicalContext,
    string Framing,
    string RuntimeStyle,
    string RuntimeExclusions,
    Guid? OwnerAccountId = null,
    Guid? SurvivorId = null,
    string? LegacyVisualFingerprint = null);
