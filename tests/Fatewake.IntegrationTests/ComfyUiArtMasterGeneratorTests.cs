using Fatewake.Infrastructure.Art;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Fatewake.IntegrationTests;

/// <summary>Verifies optional ComfyUI API-key authentication and that keys never follow redirects off the ComfyUI host.</summary>
/// <see href="../../../docs/code/tests/Fatewake.IntegrationTests/ComfyUiArtMasterGeneratorTests.md">ComfyUiArtMasterGeneratorTests documentation</see>
public sealed class ComfyUiArtMasterGeneratorTests : IDisposable
{
    private readonly string _workflow = Path.Combine(Path.GetTempPath(), $"fatewake-comfy-{Guid.NewGuid():N}.json");

    /// <summary>Creates a minimal API workflow containing the Fatewake substitution tokens.</summary>
    public ComfyUiArtMasterGeneratorTests() =>
        File.WriteAllText(_workflow, """{"1":{"inputs":{"text":"__FATEWAKE_PROMPT__","neg":"__FATEWAKE_NEGATIVE__","seed":__FATEWAKE_SEED__}}}""");

    /// <summary>A configured key is sent to every ComfyUI endpoint but stripped from the signed storage redirect.</summary>
    [Fact]
    public async Task Api_key_is_sent_to_comfyui_but_not_to_redirect_target()
    {
        var handler = new RecordingComfyUiHandler();
        var result = await Generate(handler, apiKey: "test-key", header: "X-API-Key");

        Assert.Equal(RecordingComfyUiHandler.ImageBytes, result.MasterPng);
        var comfy = handler.Requests.Where(r => r.Uri.Host == "comfy.test").ToList();
        Assert.Equal(3, comfy.Count);
        Assert.All(comfy, r => Assert.Equal("test-key", r.Headers["X-API-Key"]));
        var storage = Assert.Single(handler.Requests, r => r.Uri == RecordingComfyUiHandler.StorageUrl);
        Assert.False(storage.Headers.ContainsKey("X-API-Key"));
    }

    /// <summary>The <c>Authorization</c> header option sends the key as a bearer token.</summary>
    [Fact]
    public async Task Authorization_header_uses_bearer_scheme()
    {
        var handler = new RecordingComfyUiHandler();
        await Generate(handler, apiKey: "test-key", header: "Authorization");

        Assert.All(handler.Requests.Where(r => r.Uri.Host == "comfy.test"), r => Assert.Equal("Bearer test-key", r.Headers["Authorization"]));
        Assert.False(handler.Requests.Single(r => r.Uri == RecordingComfyUiHandler.StorageUrl).Headers.ContainsKey("Authorization"));
    }

    /// <summary>The Worker interface resolves its configured typed client even when OpenAI fallback is disabled.</summary>
    [Fact]
    public async Task Registered_generator_preserves_base_address_without_fallback()
    {
        var settings = new Dictionary<string, string?>
        {
            ["ArtGeneration:ComfyUI:Enabled"] = "true",
            ["ArtGeneration:ComfyUI:BaseUrl"] = "https://comfy.test/api/",
            ["ArtGeneration:ComfyUI:WorkflowPath"] = _workflow,
            ["ArtGeneration:ComfyUI:ApiKey"] = "test-key",
            ["ArtGeneration:ComfyUI:ApiKeyHeader"] = "X-API-Key",
            ["ArtGeneration:OpenAI:Enabled"] = "false"
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.Configure<ComfyUiArtGenerationOptions>(configuration.GetSection(ComfyUiArtGenerationOptions.SectionName));
        services.AddSingleton<IArtBinaryStorage>(new FileSystemArtBinaryStorage(Path.GetTempPath()));
        services.AddComfyUiMasterGenerator(configuration);
        var handler = new RecordingComfyUiHandler();
        services.AddHttpClient<ComfyUiArtMasterGenerator>().ConfigurePrimaryHttpMessageHandler(() => handler);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var generator = scope.ServiceProvider.GetRequiredService<IArtMasterGenerator>();
        var result = await generator.GenerateAsync(new("registered-client",
            new("asset", "Background", "fingerprint", "test prompt", "ComfyUI", "workflow")), TestContext.Current.CancellationToken);
        Assert.Equal(RecordingComfyUiHandler.ImageBytes, result.MasterPng);
        Assert.Contains(handler.Requests, request => request.Uri == new Uri("https://comfy.test/api/prompt"));
    }

    /// <summary>A null, empty, or whitespace key is treated as not configured and sends no auth header.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Unconfigured_key_sends_no_auth_header(string? apiKey)
    {
        Assert.Null(new ComfyUiArtGenerationOptions { ApiKey = apiKey }.ApiKey);
        var handler = new RecordingComfyUiHandler();
        await Generate(handler, apiKey, header: "X-API-Key");

        Assert.All(handler.Requests, r => Assert.False(r.Headers.ContainsKey("X-API-Key")));
    }

    /// <inheritdoc />
    public void Dispose() => File.Delete(_workflow);

    private Task<GeneratedArtMaster> Generate(RecordingComfyUiHandler handler, string? apiKey, string header)
    {
        var options = Options.Create(new ComfyUiArtGenerationOptions
        {
            Enabled = true,
            BaseUrl = "https://comfy.test/api/",
            ApiKey = apiKey,
            ApiKeyHeader = header,
            WorkflowPath = _workflow,
            PollIntervalMilliseconds = 10,
            TimeoutSeconds = 10
        });
        var http = new HttpClient(handler) { BaseAddress = new Uri(options.Value.BaseUrl) };
        var generator = new ComfyUiArtMasterGenerator(http, options, null!);
        var request = new ArtWorkRequest("asset", "scene", "fp", "a prompt", "ComfyUI", "model");
        return generator.GenerateAsync(new ArtGenerationInvocation("job-1", request));
    }
}
