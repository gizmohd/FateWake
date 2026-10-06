using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Fatewake.Observability;

namespace Fatewake.Web.Authentication;

/// <summary>Issues canonical browser sessions for password, mailbox proof, and provider exchanges.</summary>
/// <see href="../../../docs/code/src/Fatewake.Web/Authentication/BrowserAccountSession.md">Documentation</see>
public static class BrowserAccountSession
{
    /// <summary>Reads the API session and account metadata, protects the token in a cookie, and selects password setup when needed.</summary>
    public static async Task<string> SignInAsync(HttpContext context, HttpClient client, HttpResponseMessage response, DateTimeOffset started)
    {
        using var operation = OperationTelemetry.Start("web.issue_session");
        var token = await response.Content.ReadFromJsonAsync<LocalTokenResponse>(context.RequestAborted)
            ?? throw new InvalidDataException("The API returned no login session.");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        var account = await client.GetFromJsonAsync<LocalAccountInfo>("/api/auth/me", context.RequestAborted)
            ?? throw new InvalidDataException("The API returned no account identity.");
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, account.AccountId.ToString()),
            new Claim(ClaimTypes.Name, account.Email),
            new Claim("fatewake.api_token", token.AccessToken)
        ], CookieAuthenticationDefaults.AuthenticationScheme));
        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new AuthenticationProperties
        {
            IsPersistent = false, ExpiresUtc = started.AddSeconds(token.ExpiresIn), AllowRefresh = false
        });
        return account.HasPassword ? "/" : "/set-password";
    }
}
