using System.Security.Cryptography;
using Fatewake.Infrastructure.Persistence;
using Fatewake.Observability;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fatewake.Infrastructure.Art;

/// <summary>Stores new artwork binaries in PostgreSQL and lazily imports legacy filesystem binaries when read.</summary>
/// <param name="db">Scoped database context used for binary persistence.</param>
/// <param name="options">Provider and optional legacy filesystem root.</param>
/// <param name="log">Structured operation logging.</param>
/// <see href="../../../docs/code/src/Fatewake.Infrastructure/Art/PostgresArtBinaryStorage.md">PostgresArtBinaryStorage documentation</see>
public sealed class PostgresArtBinaryStorage(
    FatewakeDbContext db,
    IOptions<ArtStorageOptions> options,
    ILogger<PostgresArtBinaryStorage> log) : IArtBinaryStorage
{
    private readonly FileSystemArtBinaryStorage? legacyStorage = string.IsNullOrWhiteSpace(options.Value.RootPath)
        ? null
        : new FileSystemArtBinaryStorage(Path.GetFullPath(options.Value.RootPath));

    /// <inheritdoc />
    public async Task PutAsync(string storageKey, Stream content, string contentType, CancellationToken ct = default)
    {
        using var operation = OperationTelemetry.Start("art.binary.put", log);
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);
            ArgumentNullException.ThrowIfNull(content);
            ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, ct);
            var bytes = buffer.ToArray();
            var hash = Hash(bytes);
            var now = DateTimeOffset.UtcNow;
            var record = await db.ArtBinaries.SingleOrDefaultAsync(x => x.StorageKey == storageKey, ct);
            if (record is null)
            {
                db.ArtBinaries.Add(new ArtBinaryRecord
                {
                    StorageKey = storageKey,
                    ContentType = contentType,
                    ContentHash = hash,
                    Bytes = bytes,
                    ByteSize = bytes.LongLength,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }
            else
            {
                record.ContentType = contentType;
                record.ContentHash = hash;
                record.Bytes = bytes;
                record.ByteSize = bytes.LongLength;
                record.UpdatedAt = now;
            }
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            operation.Fail(ex);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct = default)
    {
        using var operation = OperationTelemetry.Start("art.binary.read", log);
        try
        {
            var bytes = await db.ArtBinaries.AsNoTracking()
                .Where(x => x.StorageKey == storageKey)
                .Select(x => x.Bytes)
                .SingleOrDefaultAsync(ct);
            if (bytes is not null)
                return new MemoryStream(bytes, writable: false);
            if (legacyStorage is null || !await legacyStorage.ExistsAsync(storageKey, ct))
                throw new FileNotFoundException("Artwork binary was not found in PostgreSQL or the configured legacy store.", storageKey);

            await using var legacy = await legacyStorage.OpenReadAsync(storageKey, ct);
            using var buffer = new MemoryStream();
            await legacy.CopyToAsync(buffer, ct);
            bytes = buffer.ToArray();
            await PutAsync(storageKey, new MemoryStream(bytes, writable: false), ContentTypeFor(storageKey), ct);
            return new MemoryStream(bytes, writable: false);
        }
        catch (Exception ex)
        {
            operation.Fail(ex);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(string storageKey, CancellationToken ct = default)
    {
        using var operation = OperationTelemetry.Start("art.binary.exists", log);
        try
        {
            if (await db.ArtBinaries.AsNoTracking().AnyAsync(x => x.StorageKey == storageKey, ct))
                return true;
            return legacyStorage is not null && await legacyStorage.ExistsAsync(storageKey, ct);
        }
        catch (Exception ex)
        {
            operation.Fail(ex);
            throw;
        }
    }


    private static string ContentTypeFor(string key)
        => Path.GetExtension(key).ToLowerInvariant() switch
        {
            ".webp" => "image/webp",
            ".png" => "image/png",
            _ => "application/octet-stream"
        };

    private static string Hash(ReadOnlySpan<byte> bytes)
        => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
