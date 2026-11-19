using Fatewake.Web.Components;
using Fatewake.Web.Components.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Fatewake.Web.Tests;

/// <summary>Verifies the public brand and player terminology while preserving existing authenticated navigation paths.</summary>
/// <see href="../../docs/code/tests/Fatewake.Web.Tests/BrandIdentityRenderTests.md">Documentation</see>
public sealed class BrandIdentityRenderTests
{
    /// <summary>Verifies Pathweaver branding and that guest and account entry points remain available.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Landing_uses_Pathweaver_identity_and_preserves_guest_and_account_entry(bool authenticated)
    {
        var services = Services(authenticated);
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var html = await RenderAsync<Landing>(renderer, ParameterView.Empty);
        Assert.Contains("Pathweaver", html);
        Assert.Contains("Every choice shapes reality.", html);
        Assert.Contains("those who find the path", html);
        Assert.Contains("those who weave it", html);
        Assert.Contains("THEPATHWEAVER.COM", html);
        Assert.Contains("A Wayfinder Universe Experience", html);
        Assert.Contains("href=\"/play\"", html);
        Assert.Contains("Chapters", html);
        Assert.Contains("Acts", html);
        Assert.Contains("Threads", html);
        Assert.Contains("Convergences", html);
        Assert.DoesNotContain("Fatewake", html);
        Assert.DoesNotContain("survivor", html, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(!authenticated, html.Contains("href=\"/login\"", StringComparison.Ordinal));
    }

    /// <summary>Verifies the Threads link appears only for accounts while saved survivor routes keep their paths.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Threads_navigation_is_account_only_without_renaming_saved_survivor_routes(bool authenticated)
    {
        var services = Services(authenticated);
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var survivor = Guid.NewGuid();
        var parameters = ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(SiteShell.SurvivorId)] = survivor,
            [nameof(SiteShell.ChildContent)] = (RenderFragment)(builder => builder.AddContent(0, "Test chapter"))
        });
        var html = await RenderAsync<SiteShell>(renderer, parameters);
        Assert.Contains("Pathweaver home", html);
        Assert.Contains($"/novel/{survivor}", html);
        Assert.Equal(authenticated, html.Contains($"/timelines/{survivor}", StringComparison.Ordinal));
        if (authenticated)
            Assert.Contains(">Threads</a>", html);
        Assert.DoesNotContain("Fatewake", html);
    }

    private static ServiceCollection Services(bool authenticated)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorizationCore();
        services.AddSingleton<AuthenticationStateProvider>(new BrandTestAuthenticationStateProvider(authenticated));
        services.AddSingleton<NavigationManager, BrandTestNavigationManager>();
        return services;
    }

    private static Task<string> RenderAsync<T>(HtmlRenderer renderer, ParameterView parameters) where T : IComponent =>
        renderer.Dispatcher.InvokeAsync(async () =>
        {
            RenderFragment content = builder =>
            {
                builder.OpenComponent<T>(0);
                builder.AddMultipleAttributes(1, parameters.ToDictionary().Select(pair =>
                    new KeyValuePair<string, object>(pair.Key, pair.Value!)));
                builder.CloseComponent();
            };
            var root = await renderer.RenderComponentAsync<CascadingAuthenticationState>(ParameterView.FromDictionary(
                new Dictionary<string, object?> { [nameof(CascadingAuthenticationState.ChildContent)] = content }));
            return root.ToHtmlString();
        });
}
