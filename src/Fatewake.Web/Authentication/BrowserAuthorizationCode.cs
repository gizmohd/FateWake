using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace Fatewake.Web.Authentication;

/// <summary>Exchanges a single-use OpenIddict code with S256 PKCE without sending tokens or codes to the browser.</summary>
/// <see href="../../../docs/code/src/Fatewake.Web/Authentication/BrowserAuthorizationCode.md">Documentation</see>
internal static class BrowserAuthorizationCode
{
    /// <summary>Completes an account proof using the no-redirect API client and issues a browser session on success.</summary>
    internal static async Task<string?> SignInAsync(HttpContext context, HttpClient client, Dictionary<string, string> proof)
    {
        var ct = context.RequestAborted;
        var configuration = context.RequestServices.GetRequiredService<IConfiguration>();
        var clientId = configuration["OpenIddictWeb:ClientId"] ?? "fatewake-web";
        var redirect = new Uri(configuration["OpenIddictWeb:RedirectUri"] ?? "https://localhost:63279/signin-oidc");
        var verifier = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var state = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var challenge = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var started = DateTimeOffset.UtcNow;
        var authorization = new Dictionary<string, string>(proof)
        {
            ["client_id"] = clientId,
            ["redirect_uri"] = redirect.AbsoluteUri,
            ["response_type"] = "code",
            ["response_mode"] = "query",
            ["scope"] = "openid fatewake-api",
            ["state"] = state,
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256"
        };
        using var authorizationContent = new FormUrlEncodedContent(authorization);
        using var response = await client.PostAsync("/connect/authorize", authorizationContent, ct);
        client.DefaultRequestHeaders.Authorization = null;
        if (response.StatusCode == HttpStatusCode.Accepted) return "/check-email";
        if (response.StatusCode == HttpStatusCode.Unauthorized) return null;
        if (response.StatusCode != HttpStatusCode.Redirect || response.Headers.Location is not { IsAbsoluteUri: true } location ||
            location.GetLeftPart(UriPartial.Path) != redirect.AbsoluteUri || location.Fragment.Length != 0)
            throw new HttpRequestException("Account authorization did not return the configured callback.", null, response.StatusCode);
        var query = QueryHelpers.ParseQuery(location.Query);
        if (!query.TryGetValue("state", out var returnedState) || returnedState.Count != 1 || returnedState[0] != state ||
            !query.TryGetValue("code", out var code) || code.Count != 1 || code[0] is not { Length: > 0 } authorizationCode)
            throw new HttpRequestException("Account authorization returned an invalid code response.");

        using var exchange = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["grant_type"] = "authorization_code",
            ["code"] = authorizationCode,
            ["code_verifier"] = verifier,
            ["redirect_uri"] = redirect.AbsoluteUri
        });
        using var token = await client.PostAsync("/connect/token", exchange, ct);
        token.EnsureSuccessStatusCode();
        return await BrowserAccountSession.SignInAsync(context, client, token, started);
    }
}
