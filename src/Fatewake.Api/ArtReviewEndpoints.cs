using System.Security.Claims;
using System.Text.Json;
using Fatewake.Infrastructure.Persistence;
using Fatewake.Infrastructure.Art;
using Microsoft.EntityFrameworkCore;
namespace Fatewake.Api;
/// <summary>Authorizes and persists scene and layer critiques from administrators.</summary>
public static class ArtReviewEndpoints
{
    /// <summary>Registers the Admin permission policy without assigning roles to ordinary users.</summary>
    public static void AddArtReviewAuthorization(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddTransient<Microsoft.AspNetCore.Authentication.IClaimsTransformation, AdminRoleClaimsTransformation>();
        services.AddScoped<Fatewake.Infrastructure.Authentication.AccountRoleService>();
        services.AddAuthorization(options => options.AddPolicy("ArtAdministrator", policy => policy.RequireAuthenticatedUser()
            .RequireRole("Admin")));
    }

    /// <summary>Maps authenticated administrator review history and correction requests.</summary>
    public static void MapArtReviews(this WebApplication app)
    {
        app.MapGet("/api/art/reviews", async (string sceneKey, FatewakeDbContext db, CancellationToken ct) =>
            Results.Ok(await db.Set<ArtReviewRecord>().AsNoTracking().Where(x => x.SceneKey == sceneKey)
                .OrderByDescending(x => x.CreatedAt).Take(50).ToArrayAsync(ct))).RequireAuthorization("ArtAdministrator");
        app.MapPost("/api/art/reviews", async (ArtReviewRequest request, ClaimsPrincipal user,
            SceneArtCatalog catalog, FatewakeDbContext db, IArtWorkJobFactory jobs, CancellationToken ct) =>
        {
            if (!Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var account)) return Results.Forbid();
            var keys = request.Versions?.Keys.ToArray() ?? [];
            if (string.IsNullOrWhiteSpace(request.SceneKey) || request.SceneKey.Length > 100 || keys.Length is < 1 or > 24 ||
                keys.Any(key => !catalog.Contains(key)) || request.Versions!.Values.Any(v => v is <= 0) ||
                string.IsNullOrWhiteSpace(request.Critique) || request.Critique.Length > 4000 || request.Correction?.Length > 4000 ||
                request.AssetKey is {} key && (!keys.Contains(key) || request.Version is null || request.Version != request.Versions[key]) ||
                request.Regenerate && (request.AssetKey is null || string.IsNullOrWhiteSpace(request.Correction)))
                return Results.BadRequest(new { error = "Select a rendered layer and provide a critique; replacement requests also need correction instructions." });
            if (request.Regenerate && !catalog.GenerationEnabled) return Results.Conflict(new { error = "Artwork generation is not configured." });
            foreach (var (reviewedKey, version) in request.Versions)
                if (version is {} v && !await db.ArtAssets.AnyAsync(x => x.Key == reviewedKey && x.Version == v && x.Status == ArtAssetStatus.Approved, ct))
                    return Results.BadRequest(new { error = "A reviewed asset version does not exist." });
            var review = new ArtReviewRecord { Id = Guid.NewGuid(), AccountId = account, SceneKey = request.SceneKey,
                AssetKey = request.AssetKey, Version = request.Version, CompositionVersions = JsonSerializer.Serialize(request.Versions),
                Critique = request.Critique.Trim(), Correction = request.Correction?.Trim(), Regenerate = request.Regenerate, CreatedAt = DateTimeOffset.UtcNow };
            db.Add(review);
            await db.SaveChangesAsync(ct);
            // The correction persists before queuing, so ordinary scene resolution retries any interrupted queue request.
            if (request.Regenerate) await jobs.CreateAsync((await SceneArtRevision.CreateAsync(catalog, db, request.AssetKey!, ct)) with {AccountId=account}, ct);
            return Results.Ok(new { review.Id, queued = request.Regenerate });
        }).RequireAuthorization("ArtAdministrator");
    }
}
