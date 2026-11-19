namespace Fatewake.Api;

/// <summary>Reports whether a requested scene layer is ready or has durable generation work.</summary>
/// <param name="Key">Authored scene-art key.</param>
/// <param name="Status">Availability state reported by the API.</param>
/// <param name="Version">Approved immutable version when ready.</param>
/// <param name="JobId">Durable work-job identifier when generation is pending or terminal.</param>
/// <see href="../../../docs/code/src/Fatewake.Api/SceneArtResult.md">SceneArtResult documentation</see>
public sealed record SceneArtResult(string Key, string Status, int? Version = null, Guid? JobId = null);
