using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace Fatewake.Web.Authentication;

/// <summary>Creates per-call API clients carrying the current circuit's server-side account token.</summary>
/// <param name="clients">Factory for the service-discovery API client.</param>
/// <param name="authentication">Current player authentication state.</param>
/// <see href="../../../docs/code/src/Fatewake.Web/Authentication/AuthenticatedApiClient.md">AuthenticatedApiClient documentation</see>
public sealed class AuthenticatedApiClient(IHttpClientFactory clients, AuthenticationStateProvider authentication)
{
    /// <summary>Gets an API client authenticated as the current player, or an anonymous client for guest play.</summary>
    /// <returns>A caller-owned client with authorization isolated to the current player.</returns>
    public async Task<HttpClient> CreateAsync()
    {
        var user = await GetCurrentUserAsync();
        var client = clients.CreateClient("fatewake-api");
        if (user.Identity?.IsAuthenticated == true && user.FindFirst("fatewake.api_token") is { } token)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Value);
        return client;
    }

    /// <summary>Reads the current principal on demand without retaining a cached authentication snapshot.</summary>
    public async Task<ClaimsPrincipal> GetCurrentUserAsync()
    {
#pragma warning disable BL0013 // This service reads a fresh snapshot on each call and never stores authentication state.
        return (await authentication.GetAuthenticationStateAsync()).User;
#pragma warning restore BL0013
    }
}
