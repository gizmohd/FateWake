namespace Fatewake.Infrastructure.Story;

/// <summary>Produces a structured scene from retrieved context; implementations hold no authority over outcomes.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Story/ISceneAuthor.md">Documentation</see>.</remarks>
public interface ISceneAuthor
{
    /// <summary>Authors one scene.</summary>
    /// <param name="context">Bounded server-built context.</param>
    /// <param name="workJobId">Durable job used for idempotent usage accounting.</param>
    /// <param name="attempt">Work attempt number.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<SceneSpec> AuthorAsync(SceneAuthoringContext context, Guid workJobId, int attempt, CancellationToken ct);
}
