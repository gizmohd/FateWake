using System.Net;
using System.Security.Claims;
using System.Net.Http.Json;
using Fatewake.Observability;
using Fatewake.Web.Authentication;
using Fatewake.Web.Components.Presentation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
namespace Fatewake.Web.Tests;
/// <summary>Checks the Admin dashboard view and its read-only role boundary.</summary>
public sealed class AdminDashboardRenderTests
{
    /// <summary>Only Admin sees the monitoring grid, usage coverage and nested calendar roots.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Dashboard_renders_admin_metrics_or_denies_access(bool admin)
    {
        var clients = new Clients();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<AuthenticationStateProvider>(new Identity(admin));
        services.AddSingleton<IHttpClientFactory>(clients);
        services.AddScoped<AuthenticatedApiClient>();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<AdminDashboardPanel>(ParameterView.Empty)).ToHtmlString());
        if (admin)
        {
            Assert.Contains("Generation &amp; AI usage", html);
            Assert.Contains("AI requests by year, month and day", html);
            Assert.Contains("2026", html);
            Assert.Contains("Find user", html);
            Assert.Contains("Unavailable", html);
            Assert.Contains("Tracking began", html);
            Assert.Contains("No image jobs match", html);
            Assert.All(clients.Requests, x => Assert.Equal(HttpMethod.Get, x.Method));
        }
        else
        {
            Assert.Contains("requires Admin access", html);
            Assert.Empty(clients.Requests);
        }
    }
    private sealed class Identity(bool admin) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, admin ? "Admin" : "User"), new Claim("fatewake.api_token", "synthetic-test-token")], "test"))));
    }
    private sealed class Clients : IHttpClientFactory
    {
        public List<(HttpMethod Method, string Path)> Requests = [];
        public HttpClient CreateClient(string name) => new(new Responses(this)) { BaseAddress = new("http://test") };
    }
    private sealed class Responses(Clients clients) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            clients.Requests.Add((request.Method, request.RequestUri!.AbsolutePath));
            var metrics = new AiMetrics(0, 0, 0, 0, 0, 0, 0, 0, null, null, null, null, null, null, null, null, null);
            object value = request.RequestUri.AbsolutePath switch
            {
                "/api/admin/summary" => new AdminDashboardSummary([], 0, 0, 0, metrics, [], DateTimeOffset.UtcNow, 0, DateTimeOffset.UtcNow),
                "/api/admin/jobs" => new AdminResultPage<AdminJobRow>([], 0, 1, 25),
                "/api/admin/usage/groups" => new[] { new AiCalendarGroup("2026", metrics, []) },
                "/api/admin/users" => Array.Empty<AdminDashboardUser>(),
                _ => throw new InvalidOperationException("Unexpected dashboard call.")
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(value, value.GetType()) });
        }
    }
}
