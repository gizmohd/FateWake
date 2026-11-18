using System.Net;
using System.Net.Http.Json;
using Fatewake.Api;
using Fatewake.Infrastructure.Art;
using Fatewake.Infrastructure.Persistence;
using Fatewake.Infrastructure.Work;
using Fatewake.Web.Presentation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fatewake.IntegrationTests;

/// <summary>Exercises the production scene-art routes without an external image provider.</summary>
public sealed class SceneArtworkTests
{
    /// <summary>Every authored scene layer has a prompt and stable, distinct identity.</summary>
    [Fact]
    public void Catalog_covers_all_day_one_layers_and_rejects_arbitrary_keys()
    {
        var catalog = new SceneArtCatalog(new ConfigurationBuilder().Build());
        var scenes = new[] { "day1-phone-0617", "day1-house-flicker", "day1-michelle-street", "day1-choice-wake",
            "day1-radio-fragment", "day1-radio-timestamp", "day1-shutdown-0643", "day1-after-shutdown" };
        var requests = scenes.SelectMany(DayOneArt.For).Select(x => x.AssetKey).Distinct().Select(catalog.Create).ToArray();
        Assert.Equal(19, requests.Length);
        Assert.Equal(requests.Length, requests.Select(x => x.VisualFingerprint).Distinct().Count());
        Assert.All(requests, request => Assert.Equal(request.VisualFingerprint, catalog.Create(request.AssetKey).VisualFingerprint));
        Assert.Contains("Latina", requests.Single(x => x.AssetKey == "char-michelle-day1-supporting").ResolvedPrompt);
        Assert.Throws<ArgumentException>(() => catalog.Create("../arbitrary"));
        var unconfiguredComfy = new SceneArtCatalog(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ArtGeneration:Provider"] = "ComfyUI", ["ArtGeneration:ComfyUI:Enabled"] = "true",
            ["ArtGeneration:ComfyUI:WorkflowPath"] = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json")
        }).Build());
        Assert.False(unconfiguredComfy.GenerationEnabled);
    }

    /// <summary>Missing artwork is explicitly disabled until a provider is configured; approved artwork still works.</summary>
    [Fact]
    public async Task Disabled_provider_reuses_approved_art_without_enqueuing()
    {
        await using var app = CreateApp(false);
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();
        var results = await Resolve(client, "bg-day1-bedroom-dark");
        Assert.Equal("disabled", Assert.Single(results).Status);
        var assets = app.Services.GetRequiredService<Assets>();
        assets.Match = new(Guid.NewGuid(), "bg-day1-bedroom-dark", 1, "master", "webp", "png");
        Assert.Equal("ready", Assert.Single(await Resolve(client, "bg-day1-bedroom-dark")).Status);
        Assert.Equal(0, app.Services.GetRequiredService<Jobs>().Calls);
    }

    /// <summary>Scene requests report durable work status, reject unknown inputs, and deliver only approved versions.</summary>
    [Fact]
    public async Task Missing_scene_is_queued_then_delivered_and_failure_is_visible()
    {
        await using var app = CreateApp(true);
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();
        var results = await Resolve(client, "bg-day1-bedroom-dark", "bg-day1-bedroom-dark");
        Assert.Equal("pending", Assert.Single(results).Status);
        Assert.Equal(1, app.Services.GetRequiredService<Jobs>().Calls);
        using (var invalid = await client.PostAsJsonAsync("/api/art/scene/resolve", new { keys = new[] { "arbitrary-prompt" } }, TestContext.Current.CancellationToken))
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using (var missing = await client.GetAsync("/api/art/bg-day1-bedroom-dark/1/png", TestContext.Current.CancellationToken))
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FatewakeDbContext>();
            (await db.WorkJobs.SingleAsync(TestContext.Current.CancellationToken)).Status = WorkJobStatus.Failed;
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        Assert.Equal("failed", Assert.Single(await Resolve(client, "bg-day1-bedroom-dark")).Status);
        app.Services.GetRequiredService<Assets>().Match = new(Guid.NewGuid(), "bg-day1-bedroom-dark", 1, "master", "webp", "png");
        Assert.Equal("ready", Assert.Single(await Resolve(client, "bg-day1-bedroom-dark")).Status);
        using var delivered = await client.GetAsync("/api/art/bg-day1-bedroom-dark/1/png", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, delivered.StatusCode);
        Assert.Equal("image/png", delivered.Content.Headers.ContentType?.MediaType);
        Assert.Equal(new byte[] { 1, 2, 3 }, await delivered.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        using var wrongVersion = await client.GetAsync("/api/art/bg-day1-bedroom-dark/2/png", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, wrongVersion.StatusCode);
    }

    private static async Task<SceneArtResult[]> Resolve(HttpClient client, params string[] keys)
    {
        using var response = await client.PostAsJsonAsync("/api/art/scene/resolve", new { keys });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SceneArtResult[]>())!;
    }

    private static WebApplication CreateApp(bool enabled)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ArtGeneration:Provider"] = enabled ? "Local" : "None",
            ["ArtGeneration:Local:Enabled"] = "true"
        });
        var database = Guid.NewGuid().ToString();
        builder.Services.AddDbContext<FatewakeDbContext>(options => options.UseInMemoryDatabase(database));
        builder.Services.AddSingleton<SceneArtCatalog>();
        builder.Services.AddSingleton<Assets>();
        builder.Services.AddSingleton<IArtAssetStore>(sp => sp.GetRequiredService<Assets>());
        builder.Services.AddScoped<IArtResolutionService, ArtResolutionService>();
        builder.Services.AddSingleton<Jobs>();
        builder.Services.AddScoped<IArtWorkJobFactory>(sp => sp.GetRequiredService<Jobs>());
        builder.Services.AddSingleton<IArtBinaryStorage, Storage>();
        var app = builder.Build();
        app.MapSceneArt();
        return app;
    }

    private sealed class Assets : IArtAssetStore
    {
        public ArtAssetMatch? Match;
        public Task<ArtAssetMatch?> FindApprovedByFingerprintAsync(string fingerprint, CancellationToken ct = default) => Task.FromResult(Match);
        public Task<ArtAssetMatch?> FindApprovedAsync(string key, int version, CancellationToken ct = default)
            => Task.FromResult(Match?.Key == key && Match.Version == version ? Match : null);
    }

    private sealed class Jobs(IServiceScopeFactory scopes) : IArtWorkJobFactory
    {
        public int Calls;
        public async Task<CreatedWorkJob> CreateAsync(ArtWorkRequest request, CancellationToken ct = default)
        {
            Calls++;
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FatewakeDbContext>();
            var existing = await db.WorkJobs.SingleOrDefaultAsync(x => x.IdempotencyKey == request.VisualFingerprint, ct);
            if (existing is not null) return new(existing.Id, true, new Dictionary<string, Guid>());
            var job = new WorkJobRecord { Id = Guid.NewGuid(), JobType = "art.asset", IdempotencyKey = request.VisualFingerprint, Payload = "{}" };
            db.WorkJobs.Add(job);
            await db.SaveChangesAsync(ct);
            return new(job.Id, false, new Dictionary<string, Guid>());
        }
    }

    private sealed class Storage : IArtBinaryStorage
    {
        public Task PutAsync(string key, Stream content, string type, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> ExistsAsync(string key, CancellationToken ct = default) => Task.FromResult(true);
        public Task<Stream> OpenReadAsync(string key, CancellationToken ct = default) => Task.FromResult<Stream>(new MemoryStream([1, 2, 3]));
    }
}
