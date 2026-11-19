using System.Security.Claims;
using System.Globalization;
using Fatewake.Api.Authentication;
using Fatewake.Infrastructure.Authentication;
using Fatewake.Infrastructure.Persistence;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Fatewake.Api;

/// <summary>Issues authorization codes only after canonical account proof and lets OpenIddict validate code redemption.</summary>
/// <see href="../../docs/code/src/Fatewake.Api/OpenIddictEndpoints.md">Documentation</see>
public static class OpenIddictEndpoints
{
    /// <summary>Identifies the issuing OAuth registration inside a validated access token.</summary>
    public const string ClientClaim = "fatewake_client";
    internal const string EpochClaim = "fatewake_access_epoch";

    /// <summary>Maps OpenIddict token, authorization, logout, and userinfo endpoints.</summary>
    /// <param name="app">Application that hosts the identity endpoints.</param>
    public static void MapOpenIddict(this WebApplication app)
    {
        app.MapPost("/connect/token", (Delegate)TokenAsync).AllowAnonymous().RequireRateLimiting("local-auth");
        app.MapMethods("/connect/authorize", [HttpMethods.Get, HttpMethods.Post], AuthorizeAsync)
            .AllowAnonymous().RequireRateLimiting("local-auth");
        app.MapMethods("/connect/logout", [HttpMethods.Get, HttpMethods.Post], (Delegate)Logout).AllowAnonymous();
        app.MapGet("/connect/userinfo", UserInfoAsync).RequireAuthorization();
    }

    /// <summary>Seeds the Fatewake API scopes and Web public client when migrations complete.</summary>
    /// <param name="services">Application service provider containing OpenIddict managers.</param>
    /// <param name="configuration">Server issuer, audience, and Web client configuration.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration, CancellationToken ct = default)
    {
        var scopes = services.GetRequiredService<IOpenIddictScopeManager>();
        foreach (var scope in new[] { "fatewake-api", Scopes.Profile, Scopes.Email, Scopes.OfflineAccess })
        {
            if (await scopes.FindByNameAsync(scope, ct) is not null)
                continue;
            await scopes.CreateAsync(new OpenIddictScopeDescriptor
            {
                Name = scope,
                DisplayName = scope == "fatewake-api" ? "Fatewake API" : scope,
                Resources = { configuration["OpenIddict:ApiAudience"] ?? "fatewake-api" }
            }, ct);
        }

        var applications = services.GetRequiredService<IOpenIddictApplicationManager>();
        var clientId = configuration["OpenIddictWeb:ClientId"] ?? "fatewake-web";
        var redirectUri = ConfiguredRedirectUri(configuration);
        var postLogoutUri = ConfiguredPostLogoutUri(configuration);
        if (await applications.FindByClientIdAsync(clientId, ct) is not { } application)
        {
            var descriptor = new OpenIddictApplicationDescriptor
            {
                ClientId = clientId,
                ClientType = ClientTypes.Public,
                ConsentType = ConsentTypes.Implicit,
                DisplayName = "Fatewake Web",
                Permissions =
                {
                    Permissions.Endpoints.Authorization,
                    Permissions.Endpoints.Token,
                    Permissions.Endpoints.EndSession,
                    Permissions.GrantTypes.AuthorizationCode,
                    Permissions.GrantTypes.RefreshToken,
                    Permissions.ResponseTypes.Code,
                    Permissions.Prefixes.Scope + Scopes.OpenId,
                    Permissions.Prefixes.Scope + Scopes.Profile,
                    Permissions.Prefixes.Scope + Scopes.Email,
                    Permissions.Prefixes.Scope + Scopes.OfflineAccess,
                    Permissions.Prefixes.Scope + "fatewake-api"
                },
                Requirements =
                {
                    Requirements.Features.ProofKeyForCodeExchange
                }
            };
            descriptor.RedirectUris.Add(redirectUri);
            descriptor.PostLogoutRedirectUris.Add(postLogoutUri);
            await applications.CreateAsync(descriptor, ct);
        }
        else
        {
            var descriptor = new OpenIddictApplicationDescriptor();
            await applications.PopulateAsync(descriptor, application, ct);
            var redirectAdded = descriptor.RedirectUris.Add(redirectUri);
            var logoutAdded = descriptor.PostLogoutRedirectUris.Add(postLogoutUri);
            if (redirectAdded || logoutAdded) await applications.UpdateAsync(application, descriptor, ct);
        }
    }

    internal static Uri ConfiguredRedirectUri(IConfiguration configuration) =>
        new(configuration["OpenIddictWeb:RedirectUri"] ?? "https://localhost:63279/signin-oidc");

    internal static Uri ConfiguredPostLogoutUri(IConfiguration configuration) =>
        new(configuration["OpenIddict:WebPostLogoutUri"] ?? "https://localhost:63279/");

    private static async Task<IResult> TokenAsync(HttpContext context, FatewakeDbContext db, CancellationToken ct)
    {
        var request = context.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request could not be parsed.");

        if (request.IsAuthorizationCodeGrantType() || request.IsRefreshTokenGrantType())
        {
            var authenticate = await context.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            if (authenticate.Succeeded && authenticate.Principal is { } principal && Guid.TryParse(principal.GetClaim(Claims.Subject), out var accountId))
            {
                var account = await CanonicalAccountAccess.FindAsync(db, accountId, ct);
                if (account is not null && MatchesEpoch(principal, account.Epoch))
                    return Results.SignIn(principal, authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }
        }

        return Results.Forbid(new AuthenticationProperties(new Dictionary<string, string?>
        {
            [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant
        }), [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
    }

    private static async Task<IResult> AuthorizeAsync(HttpContext context, LocalAccountService accounts,
        VerifiedAccountService verification, AccountRoleService roles, FatewakeDbContext db, IConfiguration configuration, CancellationToken ct)
    {
        var request = context.GetOpenIddictServerRequest();
        if (request?.ClientId is not { } clientId ||
            request.ResponseType != ResponseTypes.Code || request.ResponseMode is not (null or ResponseModes.Query) ||
            request.CodeChallengeMethod != CodeChallengeMethods.Sha256 || string.IsNullOrEmpty(request.State) ||
            !request.HasScope("fatewake-api"))
            return Results.BadRequest();

        var webClient = configuration["OpenIddictWeb:ClientId"] ?? "fatewake-web";
        if (HttpMethods.IsGet(context.Request.Method))
        {
            if (clientId == webClient) return Results.BadRequest();
            if (request.HasPromptValue(PromptValues.None))
                return Results.Forbid(new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.ConsentRequired
                }), [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
            var origin = configuration["Email:PublicWebUrl"] ?? "https://localhost:63279";
            return Results.Redirect(origin.TrimEnd('/') + "/authorize-application" + context.Request.QueryString);
        }
        var started = DateTimeOffset.UtcNow;
        var form = await context.Request.ReadFormAsync(ct);
        LocalAccountResult? account;
        if (clientId == webClient)
        {
            account = form["account_action"].ToString() switch
            {
                "login" => await accounts.LoginAsync(form["email"].ToString(), form["password"].ToString(), ct),
                "verify_email" => await verification.VerifyAsync(form["verification_token"].ToString(), ct, form["password"].ToString()),
                "external" => await ExternalAuthenticationExtensions.ResolveExternalAccountAsync(
                    context, form["provider"].ToString(), configuration, verification, ct),
                _ => null
            };
            if (account?.VerificationRequired == true) return Results.Accepted();
        }
        else
        {
            if (form["account_action"] != "consent" || context.User.Identity?.IsAuthenticated != true ||
                context.User.FindFirstValue(ClientClaim) != webClient ||
                !Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var subject))
                return Results.Unauthorized();
            account = new(subject, context.User.FindFirstValue(ClaimTypes.Email), null);
        }
        if (account?.AccountId is not { } accountId || account.Email is not { } email) return Results.Unauthorized();

        var access = await CanonicalAccountAccess.FindAsync(db, accountId, ct);
        if (access is null || access.Epoch > started || (clientId != webClient && !MatchesEpoch(context.User, access.Epoch)))
            return Results.Unauthorized();
        var role = clientId == webClient ? await roles.AssignOnSignInAsync(accountId, ct) : "User";
        var identity = CreateIdentity(accountId, email, role);
        identity.AddClaim(ClientClaim, clientId);
        if (access.Epoch is { } epoch) identity.AddClaim(EpochClaim, FormatEpoch(epoch));
        identity.SetScopes(request.GetScopes());
        identity.SetResources(configuration["OpenIddict:ApiAudience"] ?? "fatewake-api");
        SetDestinations(identity);
        return Results.SignIn(new ClaimsPrincipal(identity),
            authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    internal static bool MatchesEpoch(ClaimsPrincipal principal, DateTimeOffset? epoch) =>
        principal.FindFirstValue(EpochClaim) == (epoch is { } value ? FormatEpoch(value) : null);

    private static string FormatEpoch(DateTimeOffset epoch) =>
        epoch.UtcTicks.ToString(CultureInfo.InvariantCulture);

    private static IResult Logout() => Results.SignOut(
        new AuthenticationProperties { RedirectUri = "/" }, [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);

    private static async Task UserInfoAsync(HttpContext context)
    {
        var principal = context.User;
        await Results.Ok(new
        {
            sub = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue(Claims.Subject),
            name = principal.FindFirstValue(ClaimTypes.Name) ?? principal.FindFirstValue(Claims.Name),
            email = principal.FindFirstValue(ClaimTypes.Email) ?? principal.FindFirstValue(Claims.Email),
            role = principal.FindFirstValue(ClaimTypes.Role) ?? principal.FindFirstValue(Claims.Role)
        }).ExecuteAsync(context);
    }

    private static ClaimsIdentity CreateIdentity(Guid accountId, string email, string role)
    {
        var identity = new ClaimsIdentity(
            authenticationType: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            nameType: Claims.Name,
            roleType: Claims.Role);
        identity.AddClaim(Claims.Subject, accountId.ToString());
        identity.AddClaim(Claims.Name, email);
        identity.AddClaim(Claims.Email, email);
        identity.AddClaim(Claims.Role, role);
        identity.AddClaim(ClaimTypes.NameIdentifier, accountId.ToString());
        identity.AddClaim(ClaimTypes.Email, email);
        identity.AddClaim(ClaimTypes.Name, email);
        identity.AddClaim(ClaimTypes.Role, role);
        return identity;
    }

    private static void SetDestinations(ClaimsIdentity identity)
    {
        foreach (var claim in identity.Claims)
        {
            claim.SetDestinations(claim.Type switch
            {
                Claims.Subject or Claims.Name or Claims.Email or Claims.Role => [Destinations.AccessToken, Destinations.IdentityToken],
                ClaimTypes.NameIdentifier or ClaimTypes.Email or ClaimTypes.Name or ClaimTypes.Role => [Destinations.AccessToken],
                _ => [Destinations.AccessToken]
            });
        }
    }

}
