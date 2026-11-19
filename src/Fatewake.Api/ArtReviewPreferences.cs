using Fatewake.Infrastructure.Authentication;
using Fatewake.Infrastructure.Persistence;
using Fatewake.Infrastructure.Story;
using Microsoft.EntityFrameworkCore;

namespace Fatewake.Api;

internal static class ArtReviewPreferences
{
    public static async Task<string[]> LoadAsync(FatewakeDbContext db, string key, CancellationToken ct)
    {
        var recent = await db.Set<ArtReviewRecord>().AsNoTracking()
            .Where(review => review.UseForFutureArtwork)
            .OrderByDescending(review => review.AssetKey == key ||
                (review.AssetKey == null && EF.Functions.JsonExists(review.CompositionVersions, key)))
            .ThenByDescending(review => review.CreatedAt).ThenByDescending(review => review.Id)
            .Select(review => review.Correction ?? review.Critique).Take(20).ToArrayAsync(ct);
        return recent.Select(VisualNote).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).Take(4).ToArray();
    }

    public static string? VisualNote(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || !StoryContentPolicy.Allows(ContentLevel.AdultClean, text))
            return null;
        var normalized = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        var length = Math.Min(600, normalized.Length);
        if (length > 0 && char.IsHighSurrogate(normalized[length - 1]))
            length--;
        return normalized[..length];
    }
}
