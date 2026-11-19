namespace Fatewake.Infrastructure.Story;

/// <summary>Durable work queue and step names for scene generation.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Story/SceneWork.md">Documentation</see>.</remarks>
public static class SceneWork
{
    /// <summary>Queue name; must be listed in <c>Work:QueueConcurrency</c>.</summary>
    public const string Queue = "story.scene";

    /// <summary>Step type handled by <see cref="SceneWorkStepHandler"/>.</summary>
    public const string StepType = "story.scene.generate";

    /// <summary>Job type.</summary>
    public const string JobType = "story.scene";

    /// <summary>Private RAG timeline key for a survivor timeline's generated story.</summary>
    public static string TimelineKey(Guid timelineId) => $"timeline-{timelineId:N}";
}
