using Fatewake.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fatewake.Infrastructure.Authentication;

/// <summary>Projects active account authorization state without loading credentials or tracked entities.</summary>
/// <see href="../../../docs/code/src/Fatewake.Infrastructure/Authentication/CanonicalAccountAccess.md">Documentation</see>
public sealed record CanonicalAccountAccess(string Role, DateTimeOffset? Epoch)
{
    /// <summary>Reads the persisted role and revocation boundary in a single PostgreSQL round trip.</summary>
    public static Task<CanonicalAccountAccess?> FindAsync(FatewakeDbContext db, Guid accountId, CancellationToken ct = default) =>
        db.Database.SqlQuery<CanonicalAccountAccess>($"""
            SELECT COALESCE(r."Role", 'User') AS "Role", a."AccessRevokedAt" AS "Epoch"
            FROM account a LEFT JOIN account_role r ON r."AccountId" = a."Id"
            WHERE a."Id" = {accountId} AND a."Status" = {(int)AccountStatus.Active}
            """).SingleOrDefaultAsync(ct);
}
