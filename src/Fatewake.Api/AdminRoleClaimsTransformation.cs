using System.Security.Claims;
using Fatewake.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
namespace Fatewake.Api;
/// <summary>Uses canonical persisted roles, replacing stale token or external-provider role claims.</summary>
public sealed class AdminRoleClaimsTransformation(FatewakeDbContext db) : IClaimsTransformation
{
    /// <inheritdoc />
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true ||
            !Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var account)) return principal;
        var role = await db.Set<AccountRoleRecord>().AsNoTracking().Where(x => x.AccountId == account)
            .Select(x => x.Role).SingleOrDefaultAsync() ?? "User";
        var copy = principal.Clone();
        foreach (var identity in copy.Identities)
            foreach (var claim in identity.FindAll(identity.RoleClaimType).ToArray()) identity.RemoveClaim(claim);
        var authenticated = copy.Identities.First(x => x.IsAuthenticated);
        authenticated.AddClaim(new Claim(authenticated.RoleClaimType, role));
        return copy;
    }
}
