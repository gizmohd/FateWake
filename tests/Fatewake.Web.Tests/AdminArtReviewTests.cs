using System.Net;
using System.Security.Claims;
using Fatewake.Web.Authentication;
using Fatewake.Web.Components.Presentation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
namespace Fatewake.Web.Tests;
/// <summary>Verifies the Admin artwork panel is rendered only after server authorization succeeds.</summary>
public sealed class AdminArtReviewTests
{
    /// <summary>Authorized admins see editable feedback and versioned previews; ordinary users see no controls.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Review_controls_and_versioned_thumbnails_require_API_permission(bool authorized)
    {
        var services = new ServiceCollection(); services.AddLogging();
        services.AddSingleton<AuthenticationStateProvider, newState>();
        services.AddSingleton<IHttpClientFactory>(new Clients(authorized)); services.AddScoped<AuthenticatedApiClient>();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var result = await renderer.RenderComponentAsync<AdminArtReview>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                ["SceneKey"] = "day1-phone-0617",
                ["Versions"] = new Dictionary<string, int?> { ["bg-day1-bedroom-dark"] = 2 }
            })); return result.ToHtmlString();
        });
        if (authorized) { Assert.Contains("Review artwork", html); Assert.Contains("/art/bg-day1-bedroom-dark/2/webp", html); Assert.Contains("Complete composition", html); Assert.Contains("Save critique", html); }
        else Assert.DoesNotContain("Review artwork", html);
    }
    private sealed class newState : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(
            new ClaimsPrincipal(new ClaimsIdentity([new Claim("fatewake.api_token", "test-token")], "test"))));
    }
    private sealed class Clients(bool authorized) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new Responses(authorized)) { BaseAddress = new Uri("http://test") };
    }
    private sealed class Responses(bool authorized) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            return Task.FromResult(new HttpResponseMessage(authorized ? HttpStatusCode.OK : HttpStatusCode.Forbidden)
            {
                Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json")
            });
        }
    }
}
