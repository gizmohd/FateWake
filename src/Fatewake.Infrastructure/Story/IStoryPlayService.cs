namespace Fatewake.Infrastructure.Story;

/// <summary>Drives a survivor through AI-generated scenes: current scene, pre-generation, advance and decisions.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Story/IStoryPlayService.md">Documentation</see>.</remarks>
public interface IStoryPlayService
{
    /// <summary>Returns the current scene, reserving it (and the next decision-free scene) when missing.</summary>
    /// <param name="survivorId">Survivor already authorized by the caller.</param>
    /// <param name="retryFailed">Requeue the current scene when its generation failed.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<StorySceneView> GetCurrentAsync(Guid survivorId, bool retryFailed, CancellationToken ct);

    /// <summary>Completes a Ready scene that has no pending decision and returns the next scene.</summary>
    /// <exception cref="InvalidOperationException">The scene is not current, not ready, or still needs a decision.</exception>
    Task<StorySceneView> AdvanceAsync(Guid survivorId, int sequence, CancellationToken ct);

    /// <summary>Validates and records a decision, completes the scene, and returns the next scene; null when the choice was not offered.</summary>
    /// <exception cref="InvalidOperationException">The scene is not current, not ready, or has no decision.</exception>
    Task<StorySceneView?> ChooseAsync(Guid survivorId, int sequence, string actionType, string? rawInput, CancellationToken ct);
}
