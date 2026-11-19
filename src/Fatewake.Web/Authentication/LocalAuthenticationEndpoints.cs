using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.WebUtilities;
using Fatewake.Observability;

namespace Fatewake.Web.Authentication;

/// <summary>Handles antiforgery-protected browser forms and stores API tokens only inside protected HttpOnly cookies.</summary>
/// <see href="../../../docs/code/src/Fatewake.Web/Authentication/LocalAuthenticationEndpoints.md">LocalAuthenticationEndpoints documentation</see>
public static class LocalAuthenticationEndpoints
{
    /// <summary>Maps registration, login, and logout forms to same-origin endpoints.</summary>
    /// <param name="app">Web application hosting the forms.</param>
    public static void MapLocalAuthentication(this WebApplication app)
    {
        app.MapPost("/auth/login", (HttpContext context, IAntiforgery antiforgery, AuthenticatedApiClient clients,
            ILoggerFactory logs) => AuthenticateAsync(context, antiforgery, clients, logs, false)).RequireRateLimiting("local-auth");
        app.MapPost("/auth/register", (HttpContext context, IAntiforgery antiforgery, AuthenticatedApiClient clients,
            ILoggerFactory logs) => AuthenticateAsync(context, antiforgery, clients, logs, true)).RequireRateLimiting("local-auth");
        foreach (var provider in new[] { "Google", "Microsoft" })
        {
            if (!app.Configuration.GetValue<bool>("Authentication:" + provider + ":Enabled"))
                continue;
            app.MapPost("/auth/external/" + provider.ToLowerInvariant(), async (HttpContext context, IAntiforgery antiforgery, ILoggerFactory logs) =>
            {
                if (!await ValidateFormAsync(context, antiforgery, logs))
                    return Results.BadRequest("Invalid form token.");
                return Results.Challenge(new AuthenticationProperties { RedirectUri = "/play" }, [provider]);
            }).RequireRateLimiting("local-auth");
        }
        app.MapPost("/auth/verify-email", (HttpContext context, IAntiforgery antiforgery, AuthenticatedApiClient clients, ILoggerFactory logs) =>
            AccountFormAsync(context, antiforgery, clients, logs, "verify-email", "/verify-email")).RequireRateLimiting("local-auth");
        app.MapPost("/auth/resend-verification", (HttpContext context, IAntiforgery antiforgery, AuthenticatedApiClient clients, ILoggerFactory logs) =>
            AccountFormAsync(context, antiforgery, clients, logs, "resend-verification", "/check-email")).RequireRateLimiting("local-auth");
        app.MapPost("/auth/account-access", (HttpContext context, IAntiforgery antiforgery, AuthenticatedApiClient clients, ILoggerFactory logs) =>
            AccountFormAsync(context, antiforgery, clients, logs, "account-access", "/account-access")).RequireRateLimiting("local-auth");
        app.MapPost("/auth/forgot-password", (HttpContext context, IAntiforgery antiforgery, AuthenticatedApiClient clients, ILoggerFactory logs) =>
            AccountFormAsync(context, antiforgery, clients, logs, "forgot-password", "/forgot-password")).RequireRateLimiting("local-auth");
        app.MapPost("/auth/application-consent", ConsentAsync).RequireAuthorization().RequireRateLimiting("local-auth");
        app.MapPost("/auth/set-password", (HttpContext context, IAntiforgery antiforgery, AuthenticatedApiClient clients, ILoggerFactory logs) =>
            AccountFormAsync(context, antiforgery, clients, logs, "set-password", "/set-password")).RequireAuthorization().RequireRateLimiting("local-auth");
        app.MapPost("/auth/complete-profile", (HttpContext context, IAntiforgery antiforgery, AuthenticatedApiClient clients, ILoggerFactory logs) =>
            AccountFormAsync(context, antiforgery, clients, logs, "complete-profile", "/complete-profile")).RequireAuthorization().RequireRateLimiting("local-auth");
        app.MapPost("/auth/logout", async (HttpContext context, IAntiforgery antiforgery, ILoggerFactory logs) =>
        {
            if (!await ValidateFormAsync(context, antiforgery, logs))
                return Results.BadRequest("Invalid form token. Reload the account page.");
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.LocalRedirect("/");
        });
    }

    private static async Task<IResult> ConsentAsync(HttpContext context, IAntiforgery antiforgery,
        AuthenticatedApiClient clients, ILoggerFactory logs)
    {
        var log = logs.CreateLogger(typeof(LocalAuthenticationEndpoints));
        using var operation = OperationTelemetry.Start("web.application_consent", log);
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        if (!await ValidateFormAsync(context, antiforgery, logs))
            return Results.BadRequest("Invalid form token.");
        var ct = context.RequestAborted;
        var form = await context.Request.ReadFormAsync(ct);
        var fields = new Dictionary<string, string>
        {
            ["account_action"] = "consent",
            ["response_type"] = "code",
            ["response_mode"] = "query",
            ["code_challenge_method"] = "S256"
        };
        foreach (var key in new[] { "client_id", "redirect_uri", "state", "scope", "code_challenge", "nonce", "prompt" })
            if (form[key].ToString() is { Length: > 0 } value)
                fields[key] = value;
        if (!fields.TryGetValue("redirect_uri", out var redirect) || !Uri.TryCreate(redirect, UriKind.Absolute, out var callback) ||
            callback.Scheme is not ("https" or "http") || callback.Query.Length != 0 || callback.Fragment.Length != 0 ||
            !fields.TryGetValue("state", out var state))
            return Results.BadRequest("Invalid authorization request.");
        try
        {
            using var client = clients.CreateForUser(context.User, "fatewake-auth");
            using var content = new FormUrlEncodedContent(fields);
            using var response = await client.PostAsync("/connect/authorize", content, ct);
            if (response.StatusCode == HttpStatusCode.Redirect && response.Headers.Location is { IsAbsoluteUri: true } location &&
                location.GetLeftPart(UriPartial.Path) == callback.AbsoluteUri && location.Fragment.Length == 0)
            {
                var parameters = QueryHelpers.ParseQuery(location.Query);
                if (parameters["state"].Count == 1 && parameters["state"][0] == state)
                    return Results.Redirect(location.AbsoluteUri);
            }
            log.LogWarning("Application consent was rejected by the account service");
        }
        catch (HttpRequestException ex)
        {
            operation.Fail(ex);
            log.LogWarning("Application consent could not reach the account service");
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            operation.Fail(ex);
            log.LogWarning("Application consent timed out");
        }
        return Results.LocalRedirect("/authorize-application?error=invalid");
    }

    private static async Task<IResult> AuthenticateAsync(HttpContext context, IAntiforgery antiforgery,
        AuthenticatedApiClient clients, ILoggerFactory logs, bool register)
    {
        using var operationTelemetry = OperationTelemetry.Start("web.account_form");
        if (!await ValidateFormAsync(context, antiforgery, logs))
            return Results.BadRequest("Invalid form token. Reload the login page.");
        var form = await context.Request.ReadFormAsync(context.RequestAborted);
        var page = register ? "/register" : "/login";
        if (register && form["password"] != form["confirmPassword"])
            return Results.LocalRedirect(page + "?error=mismatch");
        var email = form["email"].ToString();
        var password = form["password"].ToString();
        try
        {
            using var client = clients.CreateForUser(context.User, "fatewake-auth");
            if (register)
            {
                using var response = await client.PostAsJsonAsync("/api/auth/register", new
                {
                    email,
                    password,
                    FirstName = form["firstName"].ToString(),
                    LastName = form["lastName"].ToString(),
                    City = form["city"].ToString(),
                    State = form["state"].ToString()
                }, context.RequestAborted);
                if (response.StatusCode == HttpStatusCode.Accepted)
                    return Results.LocalRedirect("/check-email");
                if (response.StatusCode == HttpStatusCode.BadRequest)
                    return Results.LocalRedirect(page + "?error=credentials");
                response.EnsureSuccessStatusCode();
                page = "/login";
            }
            var destination = await BrowserAuthorizationCode.SignInAsync(context, client, new()
            {
                ["account_action"] = "login",
                ["email"] = email,
                ["password"] = password
            });
            return Results.LocalRedirect(destination ?? page + "?error=credentials");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
        {
            return Results.LocalRedirect(page + "?error=limited");
        }
        catch (HttpRequestException ex)
        {
            operationTelemetry.Fail(ex);
            logs.CreateLogger(typeof(LocalAuthenticationEndpoints)).LogError(ex, "Account service request failed");
            return Results.LocalRedirect(page + "?error=unavailable");
        }
        catch (OperationCanceledException ex) when (!context.RequestAborted.IsCancellationRequested)
        {
            operationTelemetry.Fail(ex);
            logs.CreateLogger(typeof(LocalAuthenticationEndpoints)).LogError(ex, "Account service request timed out");
            return Results.LocalRedirect(page + "?error=unavailable");
        }
    }

    private static async Task<IResult> AccountFormAsync(HttpContext context, IAntiforgery antiforgery,
        AuthenticatedApiClient clients, ILoggerFactory logs, string operation, string page)
    {
        using var operationTelemetry = OperationTelemetry.Start("web.account_action");
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        if (!await ValidateFormAsync(context, antiforgery, logs))
            return Results.BadRequest("Invalid form token. Reload the page.");
        var form = await context.Request.ReadFormAsync(context.RequestAborted);
        if (operation is "set-password" or "account-access" && form["password"] != form["confirmPassword"])
            return Results.LocalRedirect(page + "?error=mismatch");
        try
        {
            using var client = clients.CreateForUser(context.User, "fatewake-auth");
            if (operation == "complete-profile")
            {
                using var updated = await client.PutAsJsonAsync("/api/auth/profile", new
                {
                    FirstName = form["firstName"].ToString(),
                    LastName = form["lastName"].ToString(),
                    City = form["city"].ToString(),
                    State = form["state"].ToString()
                }, context.RequestAborted);
                if (updated.StatusCode == HttpStatusCode.BadRequest)
                    return Results.LocalRedirect(page + "?error=invalid");
                updated.EnsureSuccessStatusCode();
                var account = await client.GetFromJsonAsync<LocalAccountInfo>("/api/auth/me", context.RequestAborted)
                    ?? throw new InvalidDataException("The API returned no account identity.");
                return Results.LocalRedirect(account.HasPassword ? "/play" : "/set-password");
            }
            if (operation == "verify-email")
            {
                var destination = await BrowserAuthorizationCode.SignInAsync(context, client, new()
                {
                    ["account_action"] = "verify_email",
                    ["verification_token"] = form["token"].ToString(),
                    ["password"] = form["password"].ToString()
                });
                return Results.LocalRedirect(destination ?? page + "?error=invalid");
            }
            using var response = await client.PostAsJsonAsync("/api/auth/" + operation, new
            {
                Token = operation == "account-access" ? form["token"].ToString() : null,
                Email = form["email"].ToString(),
                Password = form["password"].ToString()
            }, context.RequestAborted);
            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                return Results.LocalRedirect(page + "?error=invalid");
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                return Results.LocalRedirect(page + "?error=limited");
            response.EnsureSuccessStatusCode();
            if (operation == "account-access")
            {
                await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return Results.LocalRedirect("/login?changed=true");
            }
            if (operation == "forgot-password")
                return Results.LocalRedirect("/forgot-password?sent=true");
            return Results.LocalRedirect(operation == "set-password" ? "/account?password=added" : "/check-email?sent=true");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
        {
            return Results.LocalRedirect(page + "?error=limited");
        }
        catch (HttpRequestException ex)
        {
            operationTelemetry.Fail(ex);
            logs.CreateLogger(typeof(LocalAuthenticationEndpoints)).LogError(ex, "Account {Operation} failed", operation);
            return Results.LocalRedirect(page + "?error=unavailable");
        }
        catch (OperationCanceledException ex) when (!context.RequestAborted.IsCancellationRequested)
        {
            operationTelemetry.Fail(ex);
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
