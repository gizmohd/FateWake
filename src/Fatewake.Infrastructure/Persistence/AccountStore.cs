using Fatewake.Infrastructure.Authentication;
using Microsoft.EntityFrameworkCore;

namespace Fatewake.Infrastructure.Persistence;

/// <summary>Resolves external identities through verified email ownership; guest IDs cannot claim survivors.</summary>
/// <see href="../../../docs/code/src/Fatewake.Infrastructure/Persistence/AccountStore.md">Documentation</see>
public sealed class AccountStore(FatewakeDbContext db, VerifiedAccountService verification) : IAccountStore
{
    /// <inheritdoc />
    public async Task<AccountRecord> ResolveAsync(ExternalLogin login, Guid? guestSurvivorId, CancellationToken ct = default)
    {
        if (guestSurvivorId is not null) throw new InvalidOperationException("Guest ownership must be proven separately.");
        var result = await verification.ExternalAsync(login, ct);
        if (result.AccountId is null) throw new InvalidOperationException(result.Error ?? "Email verification is required.");
        return await db.Accounts.SingleAsync(x => x.Id == result.AccountId, ct);
    }
}
