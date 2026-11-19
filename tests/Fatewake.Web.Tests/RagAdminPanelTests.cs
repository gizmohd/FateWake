using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Fatewake.Web.Authentication;
using Fatewake.Web.Components.Presentation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Fatewake.Web.Tests;

/// <summary>Checks RAG authentication failures provide an explicit recovery link and optional error dialog.</summary>
/// <see href="../../docs/code/tests/Fatewake.Web.Tests/RagAdminPanelTests.md">Documentation</see>
public sealed class RagAdminPanelTests : AuthenticationStateProvider, IHttpClientFactory, IJSRuntime
{
    /// <summary>Represents a signed-in circuit whose access token is no longer accepted by the API.</summary>
    public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(
        new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim(AuthenticatedApiClient.AccessTokenClaim, "test-access-token")
        ], "cookie"))));

    HttpClient IHttpClientFactory.CreateClient(string name) =>
        new(new UnauthorizedApi()) { BaseAddress = new Uri("https://api.test") };

    ValueTask<TValue> IJSRuntime.InvokeAsync<TValue>(string identifier, object?[]? args) =>
        throw new InvalidOperationException("Prerender must not invoke JavaScript.");

    ValueTask<TValue> IJSRuntime.InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
        throw new InvalidOperationException("Prerender must not invoke JavaScript.");

    /// <summary>Shows useful authorization recovery instead of the former generic import failure.</summary>
    [Fact]
    public async Task Rejected_session_offers_sign_in_and_error_popup()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<AuthenticationStateProvider>(this);
        services.AddSingleton<IHttpClientFactory>(this);
        services.AddSingleton<IJSRuntime>(this);
        services.AddScoped<AuthenticatedApiClient>();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<RagAdminPanel>()).ToHtmlString());
        Assert.Contains("Your API sign-in is no longer valid", html);
        Assert.Contains("Sign in again", html);
        Assert.Contains("Show status error details", html);
        Assert.Contains("<dialog", html);
        Assert.Contains("aria-labelledby=\"rag-error-title\"", html);
        Assert.DoesNotContain("The document could not be imported.", html);
    }

    private sealed class UnauthorizedApi : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("test-access-token", request.Headers.Authorization?.Parameter);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = JsonContent.Create(new { code = "rag.sign_in_required", detail = "Sign in again.", traceId = "test-reference" })
            });
        }
    }
}
