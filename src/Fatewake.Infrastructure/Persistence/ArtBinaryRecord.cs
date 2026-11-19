namespace Fatewake.Infrastructure.Persistence;

/// <summary>Stores an immutable or staging artwork binary and its content metadata in PostgreSQL.</summary>
/// <see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/ArtBinaryRecord.md">ArtBinaryRecord documentation</see>
public sealed class ArtBinaryRecord
{
    /// <summary>Stable logical path referenced by asset and work-step metadata.</summary>
    public required string StorageKey
    {
        get; set;
    }

    /// <summary>MIME type served with the stored bytes.</summary>
    public required string ContentType
    {
        get; set;
    }

    /// <summary>Lowercase SHA-256 digest used for integrity checks and cache validators.</summary>
    public required string ContentHash
    {
        get; set;
    }

    /// <summary>Complete encoded image payload stored as PostgreSQL bytea.</summary>
    public required byte[] Bytes
    {
        get; set;
    }

    /// <summary>Encoded payload size, kept consistent with the bytea value.</summary>
    public long ByteSize
    {
        get; set;
    }

    /// <summary>UTC time when this storage key was first written.</summary>
    public DateTimeOffset CreatedAt
    {
        get; set;
    }

    /// <summary>UTC time of the latest write or verified import.</summary>
    public DateTimeOffset UpdatedAt
    {
        get; set;
    }
}
