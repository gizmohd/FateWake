using Microsoft.AspNetCore.Components;

namespace Fatewake.Web.Tests;

/// <summary>Keeps navigation local while brand tests render links without issuing real browser requests.</summary>
/// <see href="../../docs/code/tests/Fatewake.Web.Tests/BrandTestNavigationManager.md">Documentation</see>
internal sealed class BrandTestNavigationManager : NavigationManager
{
    public BrandTestNavigationManager() => Initialize("https://localhost/", "https://localhost/");

    protected override void NavigateToCore(string uri, bool forceLoad) =>
        throw new NotSupportedException("Static brand tests do not navigate.");
}
