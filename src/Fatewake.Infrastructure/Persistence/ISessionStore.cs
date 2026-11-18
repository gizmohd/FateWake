namespace Fatewake.Infrastructure.Persistence;

/// <summary>Persists guest and account-owned game sessions and presentation progress.</summary>
/// <see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/ISessionStore.md">ISessionStore documentation</see>
public interface ISessionStore
{
    /// <summary>Resumes a survivor belonging to the supplied account, or creates a new survivor; null account retains guest play.</summary>
    /// <param name="survivorId">Optional existing survivor ID, filtered by ownership.</param>
    /// <param name="broadRegion">Region recorded for a newly created survivor.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <param name="accountId">Authenticated canonical account, or null for a guest.</param>
    /// <returns>Persisted game and presentation state.</returns>
    Task<SessionState> StartOrResumeAsync(Guid? survivorId, string broadRegion, CancellationToken ct = default, Guid? accountId = null);

    /// <summary>Saves presentation progress after the caller has authorized survivor access.</summary>
    /// <param name="survivorId">Authorized survivor.</param>
    /// <param name="eventInstanceId">Event belonging to that survivor.</param>
    /// <param name="sceneKey">Scene being displayed.</param>
    /// <param name="beatKey">Current presentation beat.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Updated state, or null if the survivor/event does not exist.</returns>
    Task<SessionState?> SavePresentationProgressAsync(Guid survivorId, Guid eventInstanceId, string sceneKey, string beatKey, CancellationToken ct = default);

    /// <summary>Development-only: discards everything the survivor did in the active chapter (event instance) and rewinds it to its first beat.</summary>
    /// <remarks>Deletes the chapter's action attempts, resolutions, resulting game events, and the Wakes they created in one transaction. Survivor identity, account ownership, and other chapters are untouched. Never expose outside Development.</remarks>
    /// <param name="survivorId">Survivor whose access the caller has already authorized.</param>
    /// <param name="eventInstanceId">Active chapter belonging to that survivor.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The rewound state, or null if the survivor/active chapter does not exist.</returns>
    Task<SessionState?> ResetChapterAsync(Guid survivorId, Guid eventInstanceId, CancellationToken ct = default);
}
