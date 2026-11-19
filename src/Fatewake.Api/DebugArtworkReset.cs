#if DEBUG
using System.Security.Claims;
using Fatewake.Infrastructure.Art;
using Fatewake.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Fatewake.Api;

/// <summary>Exposes the local debug-only reset of generated artwork.</summary>
public static class DebugArtworkReset
{
    private const string Confirmation = "DELETE GENERATED ARTWORK";
    private const string StagingPrefix = "art/staging/";
    private const string RetiredJobPrefix = "artwork-reset:";
    private static readonly string[] OutputNames = ["master.png", "delivery.png", "delivery.webp"];

    /// <summary>Maps the reset for local Development hosts.</summary>
    public static void Map(WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
            return;

        app.MapPost("/api/admin/artwork/reset", ResetAsync).RequireAuthorization("IdentityAdministrator");
    }

    private static async Task<IResult> ResetAsync(ResetRequest request, FatewakeDbContext db,
        IOptions<ArtStorageOptions> options, ClaimsPrincipal user, ILoggerFactory logs, CancellationToken ct)
    {
        if (!string.Equals(request.Confirmation, Confirmation, StringComparison.Ordinal))
            return Results.BadRequest(new { error = "Type DELETE GENERATED ARTWORK to confirm the reset." });

        var logger = logs.CreateLogger("Fatewake.ArtworkReset");
        var root = options.Value.RootPath;
        var assets = db.ArtAssets.Where(asset => asset.MasterPngStorageKey.StartsWith(StagingPrefix));
        var artworkJobs = db.WorkJobs.Where(job => job.JobType == "art.asset" || job.JobType == "art.prompt");
        var stagedBinaries = db.ArtBinaries.Where(binary => binary.StorageKey.StartsWith(StagingPrefix));
        var files = new HashSet<string>(StringComparer.Ordinal);
        int assetCount, binaryCount, jobCount;
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            // Close the race between checking for active jobs and retiring their generation keys.
            await db.Database.ExecuteSqlRawAsync("""
                LOCK TABLE work_job, work_step, art_prompt, art_regeneration_request, art_asset,
                    art_asset_derivative, art_binary IN SHARE ROW EXCLUSIVE MODE NOWAIT
                """, ct);
            if (await artworkJobs.AnyAsync(job => job.Status == WorkJobStatus.Pending || job.Status == WorkJobStatus.Running, ct))
                return Busy();

            if (!string.IsNullOrWhiteSpace(root))
            {
                files.UnionWith(await stagedBinaries.Select(binary => binary.StorageKey).ToArrayAsync(ct));
                files.UnionWith(await assets.Select(asset => asset.MasterPngStorageKey).ToArrayAsync(ct));
                files.UnionWith(await db.ArtAssetDerivatives.Where(derivative => assets.Any(asset => asset.Id == derivative.ArtAssetId))
                    .Select(derivative => derivative.StorageKey).ToArrayAsync(ct));
                var ids = await artworkJobs.Where(job => job.JobType == "art.asset").Select(job => job.Id).ToArrayAsync(ct);
                foreach (var id in ids)
                    foreach (var output in OutputNames)
                        files.Add($"{StagingPrefix}{id:N}/{output}");
            }

            var now = DateTimeOffset.UtcNow;
            // Keep version numbers reserved so an old journal URL can never start serving a different image.
            assetCount = await assets.Where(asset => asset.Status != ArtAssetStatus.Purged)
                .ExecuteUpdateAsync(update => update.SetProperty(asset => asset.Status, ArtAssetStatus.Purged), ct);
            binaryCount = await stagedBinaries.ExecuteDeleteAsync(ct);
            jobCount = await artworkJobs.Where(job => !job.IdempotencyKey.StartsWith(RetiredJobPrefix))
                .ExecuteUpdateAsync(update => update.SetProperty(job => job.IdempotencyKey, job => RetiredJobPrefix + job.Id.ToString())
                    .SetProperty(job => job.UpdatedAt, now), ct);
            await db.ArtRegenerationRequests.Where(row => row.Status == "Pending" || row.Status == "Processing")
                .ExecuteUpdateAsync(update => update.SetProperty(row => row.Status, "Cancelled")
                    .SetProperty(row => row.UpdatedAt, now), ct);
            await db.Set<ArtPromptRecord>().Where(prompt => prompt.RequestedWorkJobId != null)
                .ExecuteUpdateAsync(update => update.SetProperty(prompt => prompt.RequestedWorkJobId, (Guid?)null)
                    .SetProperty(prompt => prompt.RequestedAssetId, (Guid?)null)
                    .SetProperty(prompt => prompt.RequestedRevisionId, (Guid?)null)
                    .SetProperty(prompt => prompt.RequestedRevisionNumber, (int?)null)
                    .SetProperty(prompt => prompt.UpdatedAt, now), ct);
            await db.Set<ArtPromptRecord>().Where(prompt => prompt.CurrentRevisionId == null &&
                    (prompt.Status == "failed" || prompt.Status == "pending"))
                .ExecuteUpdateAsync(update => update.SetProperty(prompt => prompt.Status, "disabled")
                    .SetProperty(prompt => prompt.AuthoringJobId, (Guid?)null)
                    .SetProperty(prompt => prompt.FailureCode, (string?)null)
                    .SetProperty(prompt => prompt.UpdatedAt, now), ct);
            await db.SurvivorVisualIdentities.Where(identity => assets.Any(asset => asset.Id == identity.ActiveArtAssetId))
                .ExecuteDeleteAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.LockNotAvailable)
        {
            return Busy();
        }

        // Older filesystem binaries are outside the transaction. Keep their original keys so a failed cleanup can be retried.
        var filesDeleted = 0;
        var fileErrors = 0;
        if (!string.IsNullOrWhiteSpace(root))
        {
            foreach (var key in files)
            {
                try
                {
                    if (DeleteLocalFile(root, key))
                        filesDeleted++;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
                {
                    fileErrors++;
                    logger.LogWarning(ex, "Could not remove a generated artwork file during the debug reset");
                }
            }
        }

        logger.LogWarning("Debug artwork reset by {AccountId} removed {Assets} image versions, {Binaries} database binaries and {Files} local files; {FileErrors} local errors",
            user.FindFirstValue(ClaimTypes.NameIdentifier), assetCount, binaryCount, filesDeleted, fileErrors);
        return Results.Ok(new { assetCount, binaryCount, jobCount, filesDeleted, fileErrors });
    }

    private static IResult Busy() =>
        Results.Conflict(new { error = "Artwork work is active. Wait for the artwork queue to finish, then retry the reset." });

    private static bool DeleteLocalFile(string root, string key)
    {
        var parts = key.Split('/');
        if (parts.Length != 4 || parts[0] != "art" || parts[1] != "staging" ||
            !Guid.TryParseExact(parts[2], "N", out _) || !OutputNames.Contains(parts[3], StringComparer.Ordinal))
            return false;

        // Reject reparse points on the path, including ancestors of the configured root.
        var file = new FileInfo(Path.Combine(Path.GetFullPath(root), string.Join(Path.DirectorySeparatorChar, parts)));
        var attributes = ReadAttributes(file.FullName);
        if (attributes is null)
            return false;
        if ((attributes.Value & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0)
            throw new IOException("The artwork path is not a regular file.");
        for (var parent = file.Directory; parent is not null; parent = parent.Parent)
        {
            if ((File.GetAttributes(parent.FullName) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("The artwork directory contains a reparse point.");
        }
        file.Delete();
        return true;
    }

    private static FileAttributes? ReadAttributes(string path)
    {
        try { return File.GetAttributes(path); }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }

    internal sealed record ResetRequest(string? Confirmation);
}
#endif
