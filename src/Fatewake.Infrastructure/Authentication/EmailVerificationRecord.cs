namespace Fatewake.Infrastructure.Authentication;

/// <summary>Expiring, single-use email proof; only the token hash is stored.</summary>
/// <see href="../../../docs/code/src/Fatewake.Infrastructure/Authentication/EmailVerificationRecord.md">Documentation</see>
public sealed class EmailVerificationRecord
{
    /// <summary>Challenge identifier.</summary>
    public Guid Id { get; set; }
    /// <summary>SHA-256 hash of the random emailed token.</summary>
    public required string TokenHash { get; set; }
    /// <summary>Delivery address.</summary>
    public required string Email { get; set; }
    /// <summary>Canonical email lookup key.</summary>
    public required string NormalizedEmail { get; set; }
    /// <summary>Pending registration hash, removed after confirmation.</summary>
    public string? PasswordHash { get; set; }
    /// <summary>Validated provider name for external mailbox proof.</summary>
    public string? Provider { get; set; }
    /// <summary>Validated provider subject for external mailbox proof.</summary>
    public string? Subject { get; set; }
    /// <summary>Provider-supplied display name.</summary>
    public string? DisplayName { get; set; }
    /// <summary>Creation time used for resend cooldown.</summary>
    public DateTimeOffset CreatedAt { get; set; }
    /// <summary>Last time at which proof can be accepted.</summary>
    public DateTimeOffset ExpiresAt { get; set; }
    /// <summary>Consumption time; consumed proofs cannot be reused.</summary>
    public DateTimeOffset? ConsumedAt { get; set; }
}
