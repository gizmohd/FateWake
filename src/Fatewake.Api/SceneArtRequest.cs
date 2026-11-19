namespace Fatewake.Api;

/// <summary>Contains a bounded set of authored artwork keys and optional selected-action context.</summary>
/// <param name="Keys">Scene-art catalog keys; the endpoint rejects keys outside its authored allowlist.</param>
/// <param name="SurvivorId">Optional survivor whose ownership is checked before work is queued.</param>
/// <param name="SelectedActionType">Optional authored action that raises pending work priority.</param>
/// <see href="../../../docs/code/src/Fatewake.Api/SceneArtRequest.md">SceneArtRequest documentation</see>
public sealed record SceneArtRequest(string[]? Keys, Guid? SurvivorId = null, string? SelectedActionType = null);
