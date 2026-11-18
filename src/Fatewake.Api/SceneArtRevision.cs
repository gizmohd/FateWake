using Fatewake.Infrastructure.Art;
using Fatewake.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
namespace Fatewake.Api;
/// <summary>Applies the latest explicit administrator correction to scene-layer generation.</summary>
public static class SceneArtRevision
{
    /// <summary>Builds a request with a durable correction revision and distinct cache identity.</summary>
    public static async Task<ArtWorkRequest> CreateAsync(SceneArtCatalog catalog, FatewakeDbContext db, string key, CancellationToken ct)
    {
        var work = catalog.Create(key);
        var review = await db.Set<ArtReviewRecord>().AsNoTracking().Where(x => x.AssetKey == key && x.Regenerate)
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).FirstOrDefaultAsync(ct);
        if (review is null) return work;
        var prompt = $"Required subject and composition correction: {review.Correction}\n\n{work.ResolvedPrompt}";
        return work with { ResolvedPrompt = prompt, VisualFingerprint = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes($"{work.VisualFingerprint}\n{review.Id}\n{prompt}"))).ToLowerInvariant() };
    }
}
