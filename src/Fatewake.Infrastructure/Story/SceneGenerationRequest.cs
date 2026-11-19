namespace Fatewake.Infrastructure.Story;

/// <summary>Durable input for one scene generation step.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Story/SceneGenerationRequest.md">Documentation</see>.</remarks>
/// <param name="SceneId">Reserved <c>generated_scene</c> row.</param>
public sealed record SceneGenerationRequest(Guid SceneId);
