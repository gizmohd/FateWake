using System.Security.Claims;
using Fatewake.Infrastructure.Authentication;
using Fatewake.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
namespace Fatewake.Api;
/// <summary>Uses canonical persisted roles, replacing stale token or external-provider role claims.</summary>
/// <see href="../../docs/code/src/Fatewake.Api/AdminRoleClaimsTransformation.md">Documentation</see>
public sealed class AdminRoleClaimsTransformation(FatewakeDbContext db, IConfiguration? configuration = null) : IClaimsTransformation
{
    /// <inheritdoc />
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true ||
            !Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var account)) return principal;
        var access = await CanonicalAccountAccess.FindAsync(db, account);
        if (access is null || !OpenIddictEndpoints.MatchesEpoch(principal, access.Epoch)) return new ClaimsPrincipal(new ClaimsIdentity());
        var role = principal.FindFirstValue(OpenIddictEndpoints.ClientClaim) is { } client &&
            client != (configuration?["OpenIddictWeb:ClientId"] ?? "fatewake-web") ? "User" : access.Role;
        var copy = principal.Clone();
        foreach (var identity in copy.Identities)
            foreach (var claim in identity.FindAll(identity.RoleClaimType).ToArray()) identity.RemoveClaim(claim);
        var authenticated = copy.Identities.First(x => x.IsAuthenticated);
        authenticated.AddClaim(new Claim(authenticated.RoleClaimType, role));
        return copy;
    }
}
