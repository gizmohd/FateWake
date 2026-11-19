using System.Security.Claims;
using Fatewake.Infrastructure.Authentication;
using Fatewake.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;

namespace Fatewake.Api.Authentication;

/// <summary>Validates Google/Microsoft ID tokens independently of the Web server before resolving accounts.</summary>
/// <see href="../../../docs/code/src/Fatewake.Api/Authentication/ExternalAuthenticationExtensions.md">Documentation</see>
public static class ExternalAuthenticationExtensions
{
    /// <summary>Registers only configured provider token-validation schemes.</summary>
    public static AuthenticationBuilder AddExternalTokenValidation(this AuthenticationBuilder authentication, IConfiguration configuration)
    {
        foreach (var provider in new[] { "Google", "Microsoft" })
        {
            var section = configuration.GetSection("Authentication:" + provider);
            if (!section.GetValue<bool>("Enabled")) continue;
            var audience = section["ClientId"];
            if (string.IsNullOrWhiteSpace(audience)) throw new InvalidOperationException($"{provider} requires ClientId.");
            var authority = Authority(provider, section["TenantId"]);
            authentication.AddJwtBearer(provider, options =>
            {
                options.Authority = authority;
                options.Audience = audience;
                options.MapInboundClaims = false;
                options.TokenValidationParameters.ValidateIssuer = true;
                options.TokenValidationParameters.ValidateAudience = true;
                options.TokenValidationParameters.ValidateLifetime = true;
                options.TokenValidationParameters.ClockSkew = TimeSpan.FromMinutes(1);
            });
        }
        return authentication;
    }

    /// <summary>Resolves an enabled provider from independently validated claims, never from caller-supplied identity data.</summary>
    public static async Task<LocalAccountResult?> ResolveExternalAccountAsync(HttpContext context, string provider,
        IConfiguration configuration, VerifiedAccountService accounts, CancellationToken ct)
    {
        if (provider is not ("Google" or "Microsoft") || !configuration.GetValue<bool>("Authentication:" + provider + ":Enabled"))
            return null;
        var authenticated = await context.AuthenticateAsync(provider);
        if (!authenticated.Succeeded || authenticated.Principal is not { } user) return null;
        var subject = user.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(subject)) return null;
        var email = user.FindFirstValue("email") ?? user.FindFirstValue("preferred_username");
        var verified = provider == "Google" && user.FindFirstValue("email_verified")?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;
        return await accounts.ExternalAsync(new(provider, subject, email, verified, user.FindFirstValue("name"), null), ct);
    }

    private static string Authority(string provider, string? tenant)
    {
        if (provider == "Google") return "https://accounts.google.com";
        tenant = string.IsNullOrWhiteSpace(tenant) || tenant == "consumers" ? "9188040d-6c67-4c5b-b112-36a304b66dad" : tenant;
        if (!Guid.TryParse(tenant, out var tenantId)) throw new InvalidOperationException("Microsoft TenantId must be a tenant GUID or consumers.");
        return $"https://login.microsoftonline.com/{tenantId}/v2.0";
    }
}

