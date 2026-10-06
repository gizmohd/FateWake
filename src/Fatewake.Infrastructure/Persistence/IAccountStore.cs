namespace Fatewake.Infrastructure.Persistence;

/// <summary>Resolves external identity accounts with proven email ownership.</summary>
/// <see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/IAccountStore.md">Documentation</see>
public interface IAccountStore
{
    /// <summary>Resolves a validated provider identity; unproven guest transfer and pending mailbox proof fail explicitly.</summary>
    Task<AccountRecord> ResolveAsync(ExternalLogin login, Guid? guestSurvivorId, CancellationToken ct = default);
}
