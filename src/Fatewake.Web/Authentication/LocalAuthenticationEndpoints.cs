using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Fatewake.Web.Authentication;

/// <summary>Handles antiforgery-protected browser forms and stores API tokens only inside protected HttpOnly cookies.</summary>
/// <see href="../../../docs/code/src/Fatewake.Web/Authentication/LocalAuthenticationEndpoints.md">LocalAuthenticationEndpoints documentation</see>
public static class LocalAuthenticationEndpoints
{
    /// <summary>Maps registration, login, and logout forms to same-origin endpoints.</summary>
    /// <param name="app">Web application hosting the forms.</param>
    public static void MapLocalAuthentication(this WebApplication app)
    {
        app.MapPost("/auth/login", (HttpContext context, IAntiforgery antiforgery, IHttpClientFactory clients,
            ILoggerFactory logs) => AuthenticateAsync(context, antiforgery, clients, logs, false)).RequireRateLimiting("local-auth");
        app.MapPost("/auth/register", (HttpContext context, IAntiforgery antiforgery, IHttpClientFactory clients,
            ILoggerFactory logs) => AuthenticateAsync(context, antiforgery, clients, logs, true)).RequireRateLimiting("local-auth");
        foreach (var provider in new[] { "Google", "Microsoft" })
        {
            if (!app.Configuration.GetValue<bool>("Authentication:" + provider + ":Enabled")) continue;
            app.MapPost("/auth/external/" + provider.ToLowerInvariant(), async (HttpContext context, IAntiforgery antiforgery, ILoggerFactory logs) =>
            {
                if (!await ValidateFormAsync(context, antiforgery, logs)) return Results.BadRequest("Invalid form token.");
                return Results.Challenge(new AuthenticationProperties { RedirectUri = "/" }, [provider]);
            }).RequireRateLimiting("local-auth");
        }
        app.MapPost("/auth/verify-email", (HttpContext context, IAntiforgery antiforgery, IHttpClientFactory clients, ILoggerFactory logs) =>
            AccountFormAsync(context, antiforgery, clients, logs, "verify-email", "/verify-email")).RequireRateLimiting("local-auth");
        app.MapPost("/auth/resend-verification", (HttpContext context, IAntiforgery antiforgery, IHttpClientFactory clients, ILoggerFactory logs) =>
            AccountFormAsync(context, antiforgery, clients, logs, "resend-verification", "/check-email")).RequireRateLimiting("local-auth");
        app.MapPost("/auth/set-password", (HttpContext context, IAntiforgery antiforgery, IHttpClientFactory clients, ILoggerFactory logs) =>
            AccountFormAsync(context, antiforgery, clients, logs, "set-password", "/set-password")).RequireAuthorization().RequireRateLimiting("local-auth");
        app.MapPost("/auth/logout", async (HttpContext context, IAntiforgery antiforgery, ILoggerFactory logs) =>
        {
            if (!await ValidateFormAsync(context, antiforgery, logs)) return Results.BadRequest("Invalid form token. Reload the account page.");
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.LocalRedirect("/");
        });
    }

    private static async Task<IResult> AuthenticateAsync(HttpContext context, IAntiforgery antiforgery,
        IHttpClientFactory clients, ILoggerFactory logs, bool register)
    {
        if (!await ValidateFormAsync(context, antiforgery, logs)) return Results.BadRequest("Invalid form token. Reload the login page.");
        var form = await context.Request.ReadFormAsync(context.RequestAborted);
        var page = register ? "/register" : "/login";
        if (register && form["password"] != form["confirmPassword"])
            return Results.LocalRedirect(page + "?error=mismatch");
        using var client = clients.CreateClient("fatewake-api");
        var loginStarted = DateTimeOffset.UtcNow;
        try
        {
            using var response = await client.PostAsJsonAsync(register ? "/api/auth/register" : "/api/auth/login",
                new { Email = form["email"].ToString(), Password = form["password"].ToString() }, context.RequestAborted);
            if (response.StatusCode is HttpStatusCode.Accepted or HttpStatusCode.Forbidden)
                return Results.LocalRedirect("/check-email");
            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized)
                return Results.LocalRedirect(page + "?error=credentials");
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                return Results.LocalRedirect(page + "?error=limited");
            response.EnsureSuccessStatusCode();
            return Results.LocalRedirect(await BrowserAccountSession.SignInAsync(context, client, response, loginStarted));
        }
        catch (HttpRequestException ex)
        {
            logs.CreateLogger(typeof(LocalAuthenticationEndpoints)).LogError(ex, "Account service request failed");
            return Results.LocalRedirect(page + "?error=unavailable");
        }
        catch (OperationCanceledException ex) when (!context.RequestAborted.IsCancellationRequested)
        {
            logs.CreateLogger(typeof(LocalAuthenticationEndpoints)).LogError(ex, "Account service request timed out");
            return Results.LocalRedirect(page + "?error=unavailable");
        }
    }

    private static async Task<IResult> AccountFormAsync(HttpContext context, IAntiforgery antiforgery,
        IHttpClientFactory clients, ILoggerFactory logs, string operation, string page)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        if (!await ValidateFormAsync(context, antiforgery, logs)) return Results.BadRequest("Invalid form token. Reload the page.");
        var form = await context.Request.ReadFormAsync(context.RequestAborted);
        if (operation == "set-password" && form["password"] != form["confirmPassword"])
            return Results.LocalRedirect(page + "?error=mismatch");
        using var client = clients.CreateClient("fatewake-api");
        if (operation == "set-password" && context.User.FindFirst("fatewake.api_token") is {} token)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Value);
        var started = DateTimeOffset.UtcNow;
        try
        {
            using var response = await client.PostAsJsonAsync("/api/auth/" + operation, new
            {
                Token = form["token"].ToString(), Email = form["email"].ToString(), Password = form["password"].ToString()
            }, context.RequestAborted);
            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                return Results.LocalRedirect(page + "?error=invalid");
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                return Results.LocalRedirect(page + "?error=limited");
            response.EnsureSuccessStatusCode();
            if (operation == "verify-email")
                return Results.LocalRedirect(await BrowserAccountSession.SignInAsync(context, client, response, started));
            return Results.LocalRedirect(operation == "set-password" ? "/account?password=added" : "/check-email?sent=true");
        }
        catch (HttpRequestException ex)
        {
            logs.CreateLogger(typeof(LocalAuthenticationEndpoints)).LogError(ex, "Account {Operation} failed", operation);
            return Results.LocalRedirect(page + "?error=unavailable");
        }
        catch (OperationCanceledException ex) when (!context.RequestAborted.IsCancellationRequested)
        {
            logs.CreateLogger(typeof(LocalAuthenticationEndpoints)).LogError(ex, "Account {Operation} timed out", operation);
            return Results.LocalRedirect(page + "?error=unavailable");
        }
    }

    private static async Task<bool> ValidateFormAsync(HttpContext context, IAntiforgery antiforgery, ILoggerFactory logs)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(context);
            return true;
        }
        catch (AntiforgeryValidationException ex)
        {
            logs.CreateLogger(typeof(LocalAuthenticationEndpoints)).LogWarning(ex, "Account form antiforgery validation failed");
            return false;
        }
    }
}
