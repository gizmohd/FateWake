namespace Fatewake.Web.Presentation;

/// <summary>Identifies the survivor and reusable base artwork for their visual identity.</summary>
/// <param name="SurvivorId">Survivor associated with the identity asset.</param>
/// <param name="IdentityAssetKey">Stable key for the base identity artwork.</param>
/// <param name="IdentityAssetVersion">Version of the base identity artwork.</param>
/// <see href="../../../docs/code/src/Fatewake.Web/Presentation/SurvivorVisualIdentity.md">SurvivorVisualIdentity documentation</see>
public sealed record SurvivorVisualIdentity(Guid SurvivorId, string IdentityAssetKey, int IdentityAssetVersion);
