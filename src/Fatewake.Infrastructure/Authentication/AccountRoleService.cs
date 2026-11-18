using Fatewake.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace Fatewake.Infrastructure.Authentication;
/// <summary>Atomically bootstraps the first successfully signed-in account as Admin; every later account is User.</summary>
public sealed class AccountRoleService(FatewakeDbContext db)
{
    /// <summary>Assigns and returns a stable persisted role after authentication and email verification succeed.</summary>
    public async Task<string> AssignOnSignInAsync(Guid accountId, CancellationToken ct = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Serialize the one-time bootstrap across API processes and concurrent sign-ins.
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(861017234)", ct);
        var existing = await db.Set<AccountRoleRecord>().SingleOrDefaultAsync(x => x.AccountId == accountId, ct);
        if (existing is not null) { await transaction.CommitAsync(ct); return existing.Role; }
        if (!await db.AccountEmails.AnyAsync(x => x.AccountId == accountId && x.Verified, ct))
            throw new InvalidOperationException("Role assignment requires a verified account.");
        var role = await db.Set<AccountRoleRecord>().AnyAsync(x => x.Role == "Admin", ct) ? "User" : "Admin";
        db.Add(new AccountRoleRecord { AccountId = accountId, Role = role, AssignedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return role;
    }
}
