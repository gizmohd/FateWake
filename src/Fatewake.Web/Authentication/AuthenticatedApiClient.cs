using System.Net.Http.Headers;
using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace Fatewake.Web.Authentication;

/// <summary>Creates per-call API clients carrying the current circuit's server-side account token.</summary>
/// <param name="clients">Factory for the service-discovery API client.</param>
/// <param name="authentication">Current player authentication state.</param>
/// <param name="logger">Reports rejected sessions without recording tokens or user claims.</param>
/// <see href="../../../docs/code/src/Fatewake.Web/Authentication/AuthenticatedApiClient.md">AuthenticatedApiClient documentation</see>
public sealed class AuthenticatedApiClient(IHttpClientFactory clients, AuthenticationStateProvider authentication,
    ILogger<AuthenticatedApiClient> logger)
{
    /// <summary>Names the circuit-visible claim containing the API's OAuth access_token, never an upstream ID token.</summary>
    public const string AccessTokenClaim = "fatewake.api_token";

    /// <summary>Gets an API client authenticated as the current player, or an anonymous client for guest play.</summary>
    /// <returns>A caller-owned client with authorization isolated to the current player.</returns>
    public async Task<HttpClient> CreateAsync()
    {
        return CreateForUser(await GetCurrentUserAsync());
    }

    /// <summary>Forwards the explicit HTTP request principal, keeping headers isolated from factory-pooled handlers.</summary>
    public HttpClient CreateForUser(ClaimsPrincipal user, string clientName = "fatewake-api")
    {
        var signedIn = user.Identity?.IsAuthenticated == true;
        var token = signedIn ? user.FindFirst(AccessTokenClaim)?.Value : null;
        if (signedIn && string.IsNullOrWhiteSpace(token))
        {
            logger.LogWarning("A signed-in API request was blocked because its session has no access token");
            throw new HttpRequestException("The signed-in session has no API access token. Sign in again.", null, HttpStatusCode.Unauthorized);
        }
        var client = clients.CreateClient(clientName);
        client.DefaultRequestHeaders.Authorization = signedIn ? new AuthenticationHeaderValue("Bearer", token) : null;
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
