extern alias AppHost;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Configuration;
using Xunit;
using ComfyUiResource = AppHost::Aspire.Hosting.ComfyUiResource;

namespace Fatewake.IntegrationTests;

/// <summary>Checks opt-in local renderer configuration without launching Python or requiring a GPU.</summary>
/// <see href="../../docs/code/tests/Fatewake.IntegrationTests/ComfyUiResourceTests.md">Documentation</see>
public sealed class ComfyUiResourceTests
{
    /// <summary>Keeps remote-only developers independent of local Python paths.</summary>
    [Fact]
    public void Disabled_host_does_not_require_a_local_installation()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ComfyUIHost:Enabled"] = "false"
        });
        Assert.Null(ComfyUiResource.AddLocalComfyUi(builder));
        Assert.DoesNotContain(builder.Resources, resource => resource.Name == "comfyui");
    }

    /// <summary>Fails explicitly rather than creating an unusable executable resource.</summary>
    [Fact]
    public void Enabled_host_rejects_missing_installation()
    {
        var builder = DistributedApplication.CreateBuilder([]);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ComfyUIHost:Enabled"] = "true",
            ["ComfyUIHost:PythonExecutable"] = "",
            ["ComfyUIHost:WorkingDirectory"] = ""
        });
        Assert.Throws<InvalidOperationException>(() => ComfyUiResource.AddLocalComfyUi(builder));
    }

    /// <summary>Prevents additional arguments from overriding the managed listener.</summary>
    [Theory]
    [InlineData("--listen=0.0.0.0")]
    [InlineData("--port")]
    public void Enabled_host_rejects_endpoint_override(string argument)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"fatewake-comfy-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "main.py"), "");
            var builder = DistributedApplication.CreateBuilder([]);
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ComfyUIHost:Enabled"] = "true",
                ["ComfyUIHost:PythonExecutable"] = Environment.ProcessPath!,
                ["ComfyUIHost:WorkingDirectory"] = directory,
                ["ComfyUIHost:Arguments:0"] = argument
            });
            Assert.Throws<InvalidOperationException>(() => ComfyUiResource.AddLocalComfyUi(builder));
        }
        finally
        {
            File.Delete(Path.Combine(directory, "main.py"));
            Directory.Delete(directory);
        }
    }

    /// <summary>Exposes the configured direct endpoint without an Aspire public proxy.</summary>
    [Fact]
    public async Task Enabled_host_registers_direct_endpoint()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"fatewake-comfy-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "main.py"), "");
            var builder = DistributedApplication.CreateBuilder([]);
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ComfyUIHost:Enabled"] = "true",
                ["ComfyUIHost:PythonExecutable"] = Environment.ProcessPath!,
                ["ComfyUIHost:WorkingDirectory"] = directory,
                ["ComfyUIHost:Port"] = "8190",
                ["ComfyUIHost:Arguments:0"] = "--cpu"
            });
            var comfy = ComfyUiResource.AddLocalComfyUi(builder)!;
            var resource = Assert.IsType<ExecutableResource>(comfy.Resource);
            Assert.Equal(directory, resource.WorkingDirectory);
            var endpoint = Assert.Single(resource.Annotations.OfType<EndpointAnnotation>());
            Assert.Equal(8190, endpoint.Port);
            Assert.False(endpoint.IsProxied);
            var arguments = new List<object>();
            foreach (var annotation in resource.Annotations.OfType<CommandLineArgsCallbackAnnotation>())
                await annotation.Callback(new CommandLineArgsCallbackContext(arguments, resource, CancellationToken.None));
            Assert.Equal(["-s", "main.py", "--cpu", "--listen", "127.0.0.1", "--port", "8190"], arguments.Cast<string>());
            var consumer = builder.AddResource(new ProjectResource("consumer"))
                .WithEnvironment("ArtGeneration__ComfyUI__ApiKey", "test-cloud-key");
            ComfyUiResource.WithLocalComfyUi(consumer, comfy);
            var environment = new Dictionary<string, object>();
            foreach (var annotation in consumer.Resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
                await annotation.Callback(new EnvironmentCallbackContext(builder.ExecutionContext, consumer.Resource, environment, CancellationToken.None));
            Assert.Equal("", environment["ArtGeneration__ComfyUI__ApiKey"]);
            Assert.Equal("history", environment["ArtGeneration__ComfyUI__HistoryPath"]);
            Assert.Equal("ComfyUI", environment["ArtGeneration__Provider"]);
            Assert.Same(resource, Assert.IsType<EndpointReference>(environment["ArtGeneration__ComfyUI__BaseUrl"]).Resource);
        }
        finally
        {
            File.Delete(Path.Combine(directory, "main.py"));
            Directory.Delete(directory);
        }
    }

    /// <summary>Refuses to attach to an unrelated process occupying the configured port.</summary>
    [Fact]
    public void Enabled_host_rejects_occupied_port()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"fatewake-comfy-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            File.WriteAllText(Path.Combine(directory, "main.py"), "");
            var builder = DistributedApplication.CreateBuilder([]);
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ComfyUIHost:Enabled"] = "true",
                ["ComfyUIHost:PythonExecutable"] = Environment.ProcessPath!,
                ["ComfyUIHost:WorkingDirectory"] = directory,
                ["ComfyUIHost:Port"] = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port.ToString(System.Globalization.CultureInfo.InvariantCulture)
            });
            var error = Assert.Throws<InvalidOperationException>(() => ComfyUiResource.AddLocalComfyUi(builder));
            Assert.Contains("already in use", error.Message);
        }
        finally
        {
            File.Delete(Path.Combine(directory, "main.py"));
            Directory.Delete(directory);
        }
    }
}
