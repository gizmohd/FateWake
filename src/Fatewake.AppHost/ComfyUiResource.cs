using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Configuration;

namespace Aspire.Hosting;

/// <summary>Hosts an explicitly configured local ComfyUI Python installation as an Aspire executable resource.</summary>
/// <see href="../../docs/code/src/Fatewake.AppHost/ComfyUiResource.md">Documentation</see>
public static class ComfyUiResource
{
    /// <summary>Starts local rendering only when enabled; the loopback endpoint never receives cloud credentials.</summary>
    public static IResourceBuilder<ExecutableResource>? AddLocalComfyUi(this IDistributedApplicationBuilder builder)
    {
        var section = builder.Configuration.GetSection("ComfyUIHost");
        if (!section.GetValue<bool>("Enabled")) return null;
        var executable = section["PythonExecutable"];
        var directory = section["WorkingDirectory"];
        var port = section.GetValue<int>("Port", 8188);
        var arguments = section.GetSection("Arguments").Get<string[]>() ?? [];
        if (string.IsNullOrWhiteSpace(executable) || !Path.IsPathFullyQualified(executable) || !File.Exists(executable) ||
            string.IsNullOrWhiteSpace(directory) || !Path.IsPathFullyQualified(directory) || !Directory.Exists(directory) ||
            !File.Exists(Path.Combine(directory, "main.py")) || port is < 1024 or > 65535)
            throw new InvalidOperationException("ComfyUIHost requires an existing absolute PythonExecutable, a WorkingDirectory containing main.py, and a Port from 1024 to 65535.");
        if (arguments.Any(value => value.StartsWith("--listen", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("--port", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("ComfyUIHost owns --listen and --port; do not include them in Arguments.");
        if (System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners()
            .Any(endpoint => endpoint.Port == port))
            throw new InvalidOperationException("ComfyUIHost port is already in use. Stop the existing renderer, change ComfyUIHost:Port, or disable local hosting to use an external instance.");
        return builder.AddExecutable("comfyui", executable, directory,
                ["-s", "main.py", .. arguments, "--listen", "127.0.0.1", "--port", port.ToString(System.Globalization.CultureInfo.InvariantCulture)])
            .WithHttpEndpoint(port: port, targetPort: port, name: "http", isProxied: false)
            .WithHttpHealthCheck("/system_stats");
    }

    /// <summary>Routes generation and ComfyUI health checks to the local resource with no remote API key.</summary>
    public static IResourceBuilder<ProjectResource> WithLocalComfyUi(this IResourceBuilder<ProjectResource> resource,
        IResourceBuilder<ExecutableResource> comfy)
    {
        return resource.WithEnvironment("ArtGeneration__Provider", "ComfyUI")
            .WithEnvironment("ArtGeneration__ComfyUI__Enabled", "true")
            .WithEnvironment("ArtGeneration__ComfyUI__BaseUrl", comfy.GetEndpoint("http"))
            .WithEnvironment("ArtGeneration__ComfyUI__HistoryPath", "history")
            .WithEnvironment("ArtGeneration__ComfyUI__ApiKey", "");
    }
}
