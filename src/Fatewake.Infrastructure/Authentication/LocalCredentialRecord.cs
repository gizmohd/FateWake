namespace Fatewake.Infrastructure.Authentication;

/// <summary>Stores local password credentials and lockout state separately from the canonical account.</summary>
/// <see href="../../../docs/code/src/Fatewake.Infrastructure/Authentication/LocalCredentialRecord.md">LocalCredentialRecord documentation</see>
public sealed class LocalCredentialRecord
{
    /// <summary>Canonical account owning the credential.</summary>
    public Guid AccountId { get; set; }
    /// <summary>Case-normalized email used for unique local login lookup.</summary>
    public required string NormalizedEmail { get; set; }
    /// <summary>Versioned, salted ASP.NET Identity password hash; never the password itself.</summary>
    public required string PasswordHash { get; set; }
    /// <summary>Consecutive unsuccessful password attempts.</summary>
    public int FailedAttempts { get; set; }
    /// <summary>Time until which password login is blocked.</summary>
    public DateTimeOffset? LockoutEnd { get; set; }
}
