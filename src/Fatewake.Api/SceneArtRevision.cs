using Fatewake.Infrastructure.Art;
using Fatewake.Infrastructure.Persistence;

namespace Fatewake.Api;

/// <summary>Builds scene artwork only from an explicitly selected immutable SQL prompt revision.</summary>
/// <see href="../../../docs/code/src/Fatewake.Api/SceneArtRevision.md">SceneArtRevision documentation</see>
public static class SceneArtRevision
{
    /// <summary>Creates a pinned request without consulting critiques or an implicit latest-revision override.</summary>
    public static ArtWorkRequest Create(SceneArtCatalog catalog, string key, ArtPromptRevisionRecord revision,
        Guid? accountId = null, Guid? survivorId = null, int priority = 0, string? reachedVisualContext = null)
    {
        var metadata = catalog.Describe(key, reachedVisualContext);
        return catalog.CreateWorkRequest(metadata, revision, accountId, survivorId, priority);
    }
}
