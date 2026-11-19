namespace Fatewake.Infrastructure.Persistence;

/// <summary>Stable PostgreSQL values for the canonical account lifecycle.</summary>
/// <see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/AccountStatus.md">Documentation</see>
public enum AccountStatus
{
    /// <summary>Can sign in with verified account proof.</summary>
    Active = 1,

    /// <summary>Has no access until the invited mailbox owner sets a password.</summary>
    Invited = 2,

    /// <summary>Cannot authenticate; gameplay history and ownership are retained.</summary>
    Disabled = 3
}
