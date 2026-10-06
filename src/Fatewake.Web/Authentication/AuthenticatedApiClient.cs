using System.Net.Http.Headers;
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
        var client = clients.CreateClient("fatewake-api");
        var user = (await authentication.GetAuthenticationStateAsync()).User;
        if (user.Identity?.IsAuthenticated == true && user.FindFirst("fatewake.api_token") is {} token)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Value);
        return client;
    }
}
