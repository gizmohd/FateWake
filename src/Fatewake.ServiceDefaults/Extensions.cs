using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Provides shared service configuration for Fatewake hosted applications.
/// </summary>
/// <see href="../../docs/code/src/Fatewake.ServiceDefaults/Extensions.md">Extensions documentation</see>
public static class Extensions
{
    /// <summary>
    /// Adds common service discovery and HTTP client defaults to an application builder.
    /// </summary>
    /// <typeparam name="TBuilder">The concrete host application builder type.</typeparam>
    /// <param name="builder">The builder to configure.</param>
    /// <returns>The same builder, configured with the shared service defaults.</returns>
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddServiceDiscovery();
        builder.Services.ConfigureHttpClientDefaults(http => http.AddServiceDiscovery());
        return builder;
    }
}
