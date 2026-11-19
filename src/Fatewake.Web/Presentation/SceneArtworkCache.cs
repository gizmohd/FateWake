using Microsoft.Extensions.Caching.Memory;

namespace Fatewake.Web.Presentation;

internal readonly record struct SceneArtworkScope(Guid? Survivor, Guid? Episode, string? Action, string? Outcome);

internal sealed record SceneAsset(string Key, string Status, int? Version, bool ReplacementPending = false)
{
    public bool IsReady => Status == "ready" && Version > 0;
}

// A scoped cache lives for one interactive circuit. The API still authorizes and refreshes each scene.
internal sealed class SceneArtworkCache : IDisposable
{
    private readonly MemoryCache assets = new(new MemoryCacheOptions { SizeLimit = 128 });

    internal SceneAsset? Find(SceneArtworkScope scope, string key) =>
        assets.TryGetValue((scope, key), out SceneAsset? asset) ? asset : null;

    internal void Update(SceneArtworkScope scope, SceneAsset asset)
    {
        if (asset.IsReady)
            assets.Set((scope, asset.Key), asset, new MemoryCacheEntryOptions
            {
                Size = 1,
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2)
            });
        else
            assets.Remove((scope, asset.Key));
    }

    internal void Invalidate(SceneArtworkScope scope, SceneAsset failed)
    {
        if (Find(scope, failed.Key)?.Version == failed.Version)
            assets.Remove((scope, failed.Key));
    }

    public void Dispose() => assets.Dispose();
}
