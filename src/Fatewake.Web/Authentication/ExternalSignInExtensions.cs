using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace Fatewake.Web.Authentication;

/// <summary>Uses OIDC authorization-code/PKCE sign-in and exchanges validated ID tokens for canonical API sessions.</summary>
/// <see href="../../../docs/code/src/Fatewake.Web/Authentication/ExternalSignInExtensions.md">Documentation</see>
public static class ExternalSignInExtensions
{
    /// <summary>Adds explicitly enabled Google and Microsoft handlers with fixed callbacks and issuer validation.</summary>
    public static void AddExternalSignIn(this AuthenticationBuilder authentication, IConfiguration configuration)
    {
        foreach (var provider in new[] { "Google", "Microsoft" })
        {
            var section = configuration.GetSection("Authentication:" + provider);
            if (!section.GetValue<bool>("Enabled")) continue;
            if (string.IsNullOrWhiteSpace(section["ClientId"]) || string.IsNullOrWhiteSpace(section["ClientSecret"]))
                throw new InvalidOperationException($"{provider} requires ClientId and ClientSecret.");
            var authority = "https://accounts.google.com";
            if (provider == "Microsoft")
            {
                var tenant = section["TenantId"];
                tenant = string.IsNullOrWhiteSpace(tenant) || tenant == "consumers" ? "9188040d-6c67-4c5b-b112-36a304b66dad" : tenant;
                if (!Guid.TryParse(tenant, out var tenantId)) throw new InvalidOperationException("Microsoft TenantId must be a tenant GUID or consumers.");
                authority = $"https://login.microsoftonline.com/{tenantId}/v2.0";
            }
            authentication.AddOpenIdConnect(provider, options =>
            {
                options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.Authority = authority;
                options.ClientId = section["ClientId"];
                options.ClientSecret = section["ClientSecret"];
                options.CallbackPath = "/signin-" + provider.ToLowerInvariant();
                options.ResponseType = "code";
                options.UsePkce = true;
                options.SaveTokens = true;
                options.MapInboundClaims = false;
                options.Scope.Clear();
                options.Scope.Add("openid"); options.Scope.Add("profile"); options.Scope.Add("email");
                options.Events = new OpenIdConnectEvents
                {
                    OnTicketReceived = async context =>
                    {
                        context.HandleResponse();
                        var idToken = context.Properties?.GetTokenValue("id_token");
                        if (string.IsNullOrWhiteSpace(idToken)) throw new InvalidDataException("The provider returned no ID token.");
                        using var client = context.HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient("fatewake-api");
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", idToken);
                        var started = DateTimeOffset.UtcNow;
                        try
                        {
                            using var response = await client.PostAsync("/api/auth/external/" + provider.ToLowerInvariant(), null, context.HttpContext.RequestAborted);
                            if (response.StatusCode == HttpStatusCode.Accepted)
                            {
                                context.Response.Redirect("/check-email");
                                return;
                            }
                            response.EnsureSuccessStatusCode();
                            context.Response.Redirect(await BrowserAccountSession.SignInAsync(context.HttpContext, client, response, started));
                        }
                        catch (HttpRequestException ex)
                        {
                            context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                                .CreateLogger(typeof(ExternalSignInExtensions)).LogError(ex, "External identity exchange failed");
                            context.Response.Redirect("/login?error=external");
                        }
                        catch (OperationCanceledException ex) when (!context.HttpContext.RequestAborted.IsCancellationRequested)
                        {
                            context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                                .CreateLogger(typeof(ExternalSignInExtensions)).LogError(ex, "External identity exchange timed out");
                            context.Response.Redirect("/login?error=unavailable");
                        }
                    },
                    OnRemoteFailure = context =>
                    {
                        context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                            .CreateLogger(typeof(ExternalSignInExtensions)).LogWarning("External sign-in failed for {Provider}", provider);
                        context.HandleResponse();
                        context.Response.Redirect("/login?error=external");
                        return Task.CompletedTask;
                    }
                };
            });
        }
    }
}
