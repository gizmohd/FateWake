using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Configuration;

namespace Aspire.Hosting;

/// <summary>Forwards AppHost configuration sections, normally sourced from user secrets, to resources while masking secret values.</summary>
/// <remarks><see href="../../docs/code/src/Fatewake.AppHost/ConfigurationForwarding.md">ConfigurationForwarding documentation</see>.</remarks>
public static class ConfigurationForwarding
{
    private static readonly string[] SecretKeyNames = ["ApiKey", "ClientSecret", "Password", "PrivateKey", "Secret", "Token", "SigningKey", "EncryptionKey"];

    /// <summary>Copies every non-empty value under <paramref name="section"/> to the resource; secret-named keys become masked secret parameters.</summary>
    /// <typeparam name="T">Resource type receiving environment variables.</typeparam>
    /// <param name="resource">Resource that consumes the settings.</param>
    /// <param name="builder">AppHost builder whose configuration supplies values.</param>
    /// <param name="section">Configuration section path, for example <c>ArtGeneration</c>.</param>
    /// <returns>The same resource builder for chaining.</returns>
    public static IResourceBuilder<T> WithForwardedSection<T>(this IResourceBuilder<T> resource, IDistributedApplicationBuilder builder, string section)
        where T : IResourceWithEnvironment
    {
        foreach (var (key, value) in builder.Configuration.GetSection(section).AsEnumerable())
        {
            if (string.IsNullOrEmpty(value)) continue;
            var variable = key.Replace(":", "__");
            if (IsSecret(key))
                resource.WithEnvironment(variable, GetOrAddSecretParameter(builder, key));
            else
                resource.WithEnvironment(variable, key is "ArtGeneration:ComfyUI:WorkflowPath" or "ArtGeneration:ComfyUI:OverlayWorkflowPath" or "ArtGeneration:CatalogRoot"
                    ? Path.GetFullPath(value, Path.Combine(builder.AppHostDirectory, "..", "..")) : value);
        }
        return resource;
    }

    private static bool IsSecret(string key)
    {
        var leaf = key[(key.LastIndexOf(':') + 1)..];
        return SecretKeyNames.Any(name => leaf.EndsWith(name, StringComparison.OrdinalIgnoreCase));
    }

    private static IResourceBuilder<ParameterResource> GetOrAddSecretParameter(IDistributedApplicationBuilder builder, string key)
    {
        var name = key.Replace(":", "-").ToLowerInvariant();
        return builder.Resources.OfType<ParameterResource>().FirstOrDefault(p => p.Name == name) is { } existing
            ? builder.CreateResourceBuilder(existing)
            : builder.AddParameterFromConfiguration(name, key, secret: true);
    }
}

