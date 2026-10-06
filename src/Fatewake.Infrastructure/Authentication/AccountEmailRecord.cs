namespace Fatewake.Infrastructure.Authentication;

/// <summary>Unique canonical email ownership shared by local and external identities.</summary>
/// <see href="../../../docs/code/src/Fatewake.Infrastructure/Authentication/AccountEmailRecord.md">Documentation</see>
public sealed class AccountEmailRecord
{
    /// <summary>Canonical account owning this email.</summary>
    public Guid AccountId { get; set; }
    /// <summary>Shared case-normalized lookup key.</summary>
    public required string NormalizedEmail { get; set; }
    /// <summary>Mailbox ownership has been proven.</summary>
    public bool Verified { get; set; }
}
