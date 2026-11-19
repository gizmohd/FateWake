using System.Net;
using System.Text;
using Fatewake.Api;
using Fatewake.Infrastructure.Art;
using Fatewake.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Fatewake.IntegrationTests;

/// <summary>Checks workflow routing and prompt substitution without GPU execution.</summary>
/// <see href="../../docs/code/tests/Fatewake.IntegrationTests/ComfyUiGenerationTests.md">Documentation</see>
public sealed class ComfyUiGenerationTests
{
    /// <summary>Opt-in GPU verification of the shipped background and alpha workflows.</summary>
    [Theory]
    [InlineData("Background")]
    [InlineData("Prop")]
    public async Task Local_ComfyUI_generates_real_portrait_artwork(string type)
    {
        var repository = Environment.GetEnvironmentVariable("FATEWAKE_COMFY_SMOKE_ROOT");
        if (string.IsNullOrWhiteSpace(repository))
            Assert.Skip("A running ComfyUI and installed models are required.");
        var ct = TestContext.Current.CancellationToken;
        var ssd = Environment.GetEnvironmentVariable("FATEWAKE_COMFY_SMOKE_PROFILE") != "sd15";
        var profile = ssd ? "fatewake-ssd1b" : "fatewake";
        var outputRoot = Path.Combine(repository, "data", "art", "smoke");
        using var http = new HttpClient { BaseAddress = new("http://127.0.0.1:8188/"), Timeout = TimeSpan.FromMinutes(15) };
        var generator = new ComfyUiArtMasterGenerator(http, Options.Create(new ComfyUiArtGenerationOptions
        {
            Enabled = true,
            TimeoutSeconds = 900,
            WorkflowPath = Path.Combine(repository, "workflows", profile + "-api.json"),
            OverlayWorkflowPath = Path.Combine(repository, "workflows", profile + "-overlay-api.json")
        }), new FileSystemArtBinaryStorage(outputRoot));
        var catalog = new SceneArtCatalog(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ArtGeneration:CatalogRoot"] = Path.Combine(repository, "art"),
            ["ArtGeneration:Provider"] = "ComfyUI"
        }).Build());
        var key = type == "Background" ? "bg-day1-bedroom-dark" : "prop-radio-day1";
        using var seed = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(
            Path.Combine(repository, "art", "prompt-seeds", "day1-v1.json"), ct));
        var prompt = seed.RootElement.GetProperty("prompts").EnumerateArray()
            .Single(entry => entry.GetProperty("assetKey").GetString() == key);
        var revision = new ArtPromptRevisionRecord
        {
            Id = Guid.NewGuid(),
            RevisionNumber = 1,
            PositivePrompt = prompt.GetProperty("positivePrompt").GetString()!,
            Source = "seed"
        };
        var request = SceneArtRevision.Create(catalog, key, revision);
        var timer = System.Diagnostics.Stopwatch.StartNew();
        var image = await generator.GenerateAsync(new("fatewake-comfy-smoke-" + type, request), ct);
        timer.Stop();
        var processor = new SkiaSharpArtImageProcessor();
        var info = processor.Inspect(image.MasterPng);
        Assert.Equal(ssd ? 768 : 512, info.Width);
        Assert.Equal(ssd ? 1344 : 768, info.Height);
        if (type == "Prop")
        {
            Assert.True(info.HasAlpha);
            using var decoded = SkiaSharp.SKBitmap.Decode(image.MasterPng);
            var transparent = false;
            var visible = false;
            for (var y = 0; y < decoded.Height; y++)
                for (var x = 0; x < decoded.Width; x++)
                {
                    var alpha = decoded.GetPixel(x, y).Alpha;
                    transparent |= alpha < 16;
                    visible |= alpha > 240;
                }
            Assert.True(transparent && visible, "Overlay must contain both transparent background and visible subject pixels.");
        }
        Directory.CreateDirectory(outputRoot);
        await File.WriteAllBytesAsync(Path.Combine(outputRoot, (ssd ? "ssd1b-" : "") + type.ToLowerInvariant() + ".png"), image.MasterPng, ct);
        await File.WriteAllTextAsync(Path.Combine(outputRoot, (ssd ? "ssd1b-" : "") + type.ToLowerInvariant() + ".json"),
            System.Text.Json.JsonSerializer.Serialize(new
            {
                profile,
                type,
                elapsedSeconds = timer.Elapsed.TotalSeconds,
                width = info.Width,
                height = info.Height,
                hasAlpha = info.HasAlpha
            }), ct);
    }

    /// <summary>Overlay assets use their alpha workflow and canonical tokens are substituted before submission.</summary>
    [Theory]
    [InlineData("Background", false)]
    [InlineData("Prop", true)]
    public async Task Selects_workflow_and_substitutes_tokens(string type, bool overlay)
    {
        var root = Path.Combine(Path.GetTempPath(), "fatewake-comfy-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var ct = TestContext.Current.CancellationToken;
        try
        {
            var backgroundPath = Path.Combine(root, "background.json");
            var overlayPath = Path.Combine(root, "overlay.json");
            var template = "{\"kind\":\"KIND\",\"model\":\"__FATEWAKE_MODEL__\",\"text\":\"__FATEWAKE_PROMPT__\",\"negative\":\"__FATEWAKE_NEGATIVE__\",\"seed\":__FATEWAKE_SEED__}";
            await File.WriteAllTextAsync(backgroundPath, template.Replace("KIND", "background"), ct);
            await File.WriteAllTextAsync(overlayPath, template.Replace("KIND", "overlay"), ct);
            var handler = new Responses();
            using var http = new HttpClient(handler) { BaseAddress = new("http://localhost:8188/") };
            var generator = new ComfyUiArtMasterGenerator(http, Options.Create(new ComfyUiArtGenerationOptions
            {
                Enabled = true,
                WorkflowPath = backgroundPath,
                OverlayWorkflowPath = overlayPath
            }), new FileSystemArtBinaryStorage(root));
            var result = await generator.GenerateAsync(new("test", new("asset", type, "fingerprint", "A \"radio\"\nwith texture", "ComfyUI", "workflow-model.safetensors", NegativeInstructions: "no logos")), ct);
            Assert.Equal(new byte[] { 1, 2, 3 }, result.MasterPng);
            Assert.Contains(overlay ? "overlay" : "background", handler.Submission);
            Assert.DoesNotContain("__FATEWAKE_", handler.Submission);
            using var json = System.Text.Json.JsonDocument.Parse(handler.Submission!);
            Assert.Equal("workflow-model.safetensors", json.RootElement.GetProperty("prompt").GetProperty("model").GetString());
            Assert.Equal("A \"radio\"\nwith texture", json.RootElement.GetProperty("prompt").GetProperty("text").GetString());
            Assert.True(json.RootElement.GetProperty("prompt").GetProperty("seed").TryGetInt64(out _));
        }
        finally { Directory.Delete(root, true); }
    }

    /// <summary>A model-token workflow rejects requests that do not name an installed model.</summary>
    [Fact]
    public async Task Model_token_requires_a_model_name()
    {
        var root = Path.Combine(Path.GetTempPath(), "fatewake-comfy-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var workflowPath = Path.Combine(root, "workflow.json");
        await File.WriteAllTextAsync(workflowPath, "{\"model\":\"__FATEWAKE_MODEL__\"}", TestContext.Current.CancellationToken);
        try
        {
            using var http = new HttpClient(new Responses()) { BaseAddress = new("http://localhost:8188/") };
            var generator = new ComfyUiArtMasterGenerator(http, Options.Create(new ComfyUiArtGenerationOptions
            {
                Enabled = true,
                WorkflowPath = workflowPath
            }), new FileSystemArtBinaryStorage(root));
            await Assert.ThrowsAsync<InvalidOperationException>(() => generator.GenerateAsync(new(
                "test", new("asset", "Background", "fingerprint", "prompt", "ComfyUI", "")),
                TestContext.Current.CancellationToken));
        }
        finally { Directory.Delete(root, true); }
    }

    private sealed class Responses : HttpMessageHandler
    {
        public string? Submission;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.RequestUri!.AbsolutePath == "/prompt")
            {
                Submission = await request.Content!.ReadAsStringAsync(ct);
                return Json("{\"prompt_id\":\"test-prompt\"}");
            }
            if (request.RequestUri.AbsolutePath.StartsWith("/history/", StringComparison.Ordinal))
                return Json("{\"test-prompt\":{\"outputs\":{\"7\":{\"images\":[{\"filename\":\"test.png\"}]}}}}");
            return new(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([1, 2, 3])
            };
        }
        private static HttpResponseMessage Json(string value) => new(HttpStatusCode.OK) { Content = new StringContent(value, Encoding.UTF8, "application/json") };
    }
}
