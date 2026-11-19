using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace Fatewake.Web.Tests;

/// <summary>Supplies a synthetic signed-in or guest identity without browser credentials during static brand rendering.</summary>
/// <see href="../../docs/code/tests/Fatewake.Web.Tests/BrandTestAuthenticationStateProvider.md">Documentation</see>
internal sealed class BrandTestAuthenticationStateProvider(bool authenticated) : AuthenticationStateProvider
{
    public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
        Task.FromResult(new AuthenticationState(new ClaimsPrincipal(authenticated ? new ClaimsIdentity("test") : new ClaimsIdentity())));
}
