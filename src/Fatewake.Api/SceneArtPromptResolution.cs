using Fatewake.Infrastructure.Art;

namespace Fatewake.Api;

/// <summary>Reports SQL prompt readiness and includes a request only when an immutable revision is available.</summary>
/// <see href="../../../docs/code/src/Fatewake.Api/SceneArtPromptResolution.md">Documentation</see>
/// <param name="Status">SQL prompt state: ready, pending, disabled or failed.</param>
/// <param name="WorkRequest">Pinned image work request only when an immutable revision is ready.</param>
/// <param name="JobId">Coalesced authoring work job when one exists.</param>
/// <param name="ReplacementPending">Whether a newer revision has a runnable replacement job.</param>
public sealed record SceneArtPromptResolution(string Status, ArtWorkRequest? WorkRequest = null, Guid? JobId = null,
    bool ReplacementPending = false);
