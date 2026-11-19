using System.Net;
using System.Security.Claims;
using Fatewake.Web.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fatewake.Web.Tests;

/// <summary>Verifies circuit and HTTP requests forward API access tokens without guest downgrade or cross-user leakage.</summary>
/// <see href="../../docs/code/tests/Fatewake.Web.Tests/AuthenticatedApiClientTests.md">Documentation</see>
public sealed class AuthenticatedApiClientTests : AuthenticationStateProvider, IHttpClientFactory
{
    private ClaimsPrincipal _user = new(new ClaimsIdentity());
    private int _clientsCreated;

    /// <summary>Supplies a mutable circuit identity to verify callers do not cache principals.</summary>
    public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(_user));

    /// <summary>Creates isolated clients without any network transport or user-owned credentials.</summary>
    public HttpClient CreateClient(string name)
    {
        _clientsCreated++;
        return new HttpClient { BaseAddress = new Uri("https://api.test") };
    }

    /// <summary>Forwards the API access_token rather than an OIDC ID token and isolates guests from signed-in callers.</summary>
    [Theory]
    [InlineData("fatewake-api")]
    [InlineData("fatewake-auth")]
    public void Every_signed_in_application_client_carries_the_API_access_token(string name)
    {
        var service = new AuthenticatedApiClient(this, this, NullLogger<AuthenticatedApiClient>.Instance);
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(AuthenticatedApiClient.AccessTokenClaim, "api-access-token"),
            new Claim("id_token", "not-an-api-token")
        ], "cookie"));
        using var signedIn = service.CreateForUser(principal, name);
        using var guest = service.CreateForUser(new ClaimsPrincipal(new ClaimsIdentity()), name);
        Assert.Equal("Bearer", signedIn.DefaultRequestHeaders.Authorization?.Scheme);
        Assert.Equal("api-access-token", signedIn.DefaultRequestHeaders.Authorization?.Parameter);
        Assert.Null(guest.DefaultRequestHeaders.Authorization);
        Assert.Equal("api-access-token", signedIn.DefaultRequestHeaders.Authorization?.Parameter);
    }

    /// <summary>Rejects incomplete authenticated sessions before an anonymous request can reach the API.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Missing_access_token_never_downgrades_a_signed_in_request(string? value)
    {
        _user = new ClaimsPrincipal(new ClaimsIdentity(value is null ? [] : [new Claim(AuthenticatedApiClient.AccessTokenClaim, value)], "cookie"));
        var service = new AuthenticatedApiClient(this, this, NullLogger<AuthenticatedApiClient>.Instance);
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => service.CreateAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, error.StatusCode);
        Assert.Equal(0, _clientsCreated);
    }

    /// <summary>Reads the current circuit principal for each call and never changes an earlier caller's header.</summary>
    [Fact]
    public async Task Circuit_calls_use_the_latest_identity()
    {
        var service = new AuthenticatedApiClient(this, this, NullLogger<AuthenticatedApiClient>.Instance);
        _user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(AuthenticatedApiClient.AccessTokenClaim, "first-access-token")], "cookie"));
        using var first = await service.CreateAsync();
        _user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(AuthenticatedApiClient.AccessTokenClaim, "second-access-token")], "cookie"));
        using var second = await service.CreateAsync();
        Assert.Equal("first-access-token", first.DefaultRequestHeaders.Authorization?.Parameter);
        Assert.Equal("second-access-token", second.DefaultRequestHeaders.Authorization?.Parameter);
    }
}
