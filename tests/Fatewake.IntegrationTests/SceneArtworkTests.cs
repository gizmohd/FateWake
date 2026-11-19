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
/// <see href="../../docs/code/tests/Fatewake.IntegrationTests/SceneArtworkTests.md">Documentation</see>
public sealed class SceneArtworkTests
{
    private const string FixtureStyle = "Copper highlights and indigo shadows with readable illustrated forms.";
    private const string FixtureExclusions = "metallic gloss, muddy colors";
    private static readonly string StyleFixture = $"""
        # Fatewake Visual Style Bible - v7

        ## Runtime generation style

        {FixtureStyle}

        ## Runtime style exclusions

        {FixtureExclusions}

        ## Approval guidance

        Human-only review notes must not enter provider prompts.
        """;
    private static IEnumerable<string> AuthoredLayerKeys => new[]
    {
        "day1-phone-0617", "day1-house-flicker", "day1-michelle-street", "day1-choice-wake",
        "day1-radio-fragment", "day1-radio-timestamp", "day1-shutdown-0643", "day1-after-shutdown"
    }.SelectMany(DayOneArt.For).Select(layer => layer.AssetKey).Distinct();

    /// <summary>Every authored scene layer has a prompt and stable, distinct identity.</summary>
    [Fact]
    public void Catalog_covers_all_day_one_layers_and_rejects_arbitrary_keys()
    {
        var catalog = new SceneArtCatalog(new ConfigurationBuilder().Build());
        var requests = AuthoredLayerKeys.Select(catalog.Create).ToArray();
        Assert.Equal(19, requests.Length);
        Assert.Equal(requests.Length, requests.Select(x => x.VisualFingerprint).Distinct().Count());
        Assert.All(requests, request => Assert.Equal(request.VisualFingerprint, catalog.Create(request.AssetKey).VisualFingerprint));
        Assert.All(requests, request => Assert.Contains("32-bit-era", request.ResolvedPrompt));
        Assert.All(requests, request => Assert.Equal("4", request.PromptTemplateVersion));
        Assert.All(requests, request => Assert.DoesNotContain("anime", request.NegativeInstructions));
        Assert.All(requests, request => Assert.DoesNotContain("manga", request.NegativeInstructions));
        Assert.Contains("Latina", requests.Single(x => x.AssetKey == "char-michelle-day1-supporting").ResolvedPrompt);
        Assert.Throws<ArgumentException>(() => catalog.Create("../arbitrary"));
        var unconfiguredComfy = new SceneArtCatalog(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ArtGeneration:Provider"] = "ComfyUI",
            ["ArtGeneration:ComfyUI:Enabled"] = "true",
            ["ArtGeneration:ComfyUI:WorkflowPath"] = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json")
        }).Build());
        Assert.False(unconfiguredComfy.GenerationEnabled);
    }

    /// <summary>Every role consumes the external style contract without swallowing UI/review prose or losing continuity.</summary>
    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void Shared_style_reaches_every_layer_and_preserves_role_constraints(string newline)
    {
        WithCatalog((path, catalog) =>
        {
            File.WriteAllText(path, StyleFixture.Replace("\r\n", "\n").Replace("\n", newline));
            var requests = AuthoredLayerKeys.Select(catalog.Create).ToArray();
            Assert.All(requests, request =>
            {
                Assert.Contains(FixtureStyle, request.ResolvedPrompt);
                Assert.StartsWith(FixtureExclusions, request.NegativeInstructions);
                Assert.Equal("7", request.StyleBibleVersion);
                Assert.DoesNotContain("Human-only review notes", request.ResolvedPrompt);
                Assert.DoesNotContain("Human-only review notes", request.NegativeInstructions);
            });
            var background = requests.Single(request => request.AssetKey == "bg-day1-bedroom-dark");
            Assert.StartsWith("Empty modest contemporary suburban bedroom", background.ResolvedPrompt);
            Assert.Contains("person, people", background.NegativeInstructions);
            Assert.Contains("room, furniture, scenery", requests.Single(request => request.AssetKey == "prop-radio-day1").NegativeInstructions);
            var character = requests.Single(request => request.AssetKey == "char-michelle-day1-supporting");
            Assert.Contains("Latina", character.ResolvedPrompt);
            Assert.Contains("muted gray-blue top", character.ResolvedPrompt);
        });
    }

    /// <summary>Positive style, exclusions and declared revision all participate in reuse identity.</summary>
    [Theory]
    [InlineData("style")]
    [InlineData("exclusions")]
    [InlineData("version")]
    public void Shared_style_changes_produce_new_deterministic_request_identities(string change)
    {
        WithCatalog((path, catalog) =>
        {
            File.WriteAllText(path, StyleFixture);
            var original = catalog.Create("bg-day1-bedroom-dark");
            var revised = change switch
            {
                "style" => StyleFixture.Replace(FixtureStyle, "Clear teal shadows and warm copper highlights."),
                "exclusions" => StyleFixture.Replace(FixtureExclusions, "metallic gloss, chalky highlights"),
                "version" => StyleFixture.Replace("v7", "v8"),
                _ => throw new ArgumentOutOfRangeException(nameof(change))
            };
            File.WriteAllText(path, revised);
            var updated = catalog.Create(original.AssetKey);
            Assert.NotEqual(original.VisualFingerprint, updated.VisualFingerprint);
            Assert.Equal(updated, catalog.Create(original.AssetKey));
            if (change == "version")
            {
                Assert.Equal(original.ResolvedPrompt, updated.ResolvedPrompt);
                Assert.Equal(original.NegativeInstructions, updated.NegativeInstructions);
                Assert.NotEqual(original.StyleBibleVersion, updated.StyleBibleVersion);
            }
        });
    }

    /// <summary>Broken authored style contracts fail clearly rather than quietly reverting to hard-coded instructions.</summary>
    [Theory]
    [InlineData("missing-file")]
    [InlineData("missing-style")]
    [InlineData("missing-exclusions")]
    [InlineData("empty-style")]
    [InlineData("empty-exclusions")]
    [InlineData("duplicate-style")]
    [InlineData("duplicate-exclusions")]
    [InlineData("invalid-version")]
    public void Missing_or_malformed_style_contract_is_rejected(string problem)
    {
        WithCatalog((path, catalog) =>
        {
            if (problem == "missing-file")
            {
                File.Delete(path);
                Assert.Throws<FileNotFoundException>(() => catalog.Create("bg-day1-bedroom-dark"));
                return;
            }
            var malformed = problem switch
            {
                "missing-style" => StyleFixture.Replace("## Runtime generation style", "## Other style"),
                "missing-exclusions" => StyleFixture.Replace("## Runtime style exclusions", "## Other exclusions"),
                "empty-style" => StyleFixture.Replace(FixtureStyle, ""),
                "empty-exclusions" => StyleFixture.Replace(FixtureExclusions, ""),
                "duplicate-style" => StyleFixture + "\n## Runtime generation style\nDuplicate style.",
                "duplicate-exclusions" => StyleFixture + "\n## Runtime style exclusions\nDuplicate exclusions.",
                "invalid-version" => StyleFixture.Replace("v7", "unversioned"),
                _ => throw new ArgumentOutOfRangeException(nameof(problem))
            };
            File.WriteAllText(path, malformed);
            var error = Assert.Throws<InvalidDataException>(() => catalog.Create("bg-day1-bedroom-dark"));
            Assert.DoesNotContain(FixtureStyle, error.Message);
        });
    }

    /// <summary>Saved corrections retain the shared style and provenance while producing a distinct reusable request.</summary>
    [Fact]
    public async Task Review_correction_preserves_shared_style_and_revision_metadata()
    {
        var catalog = new SceneArtCatalog(new ConfigurationBuilder().Build());
        var original = catalog.Create("bg-day1-bedroom-dark");
        var ct = TestContext.Current.CancellationToken;
        await using var db = new FatewakeDbContext(new DbContextOptionsBuilder<FatewakeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        const string correction = "Keep the bed silhouette clear of the doorway.";
        db.Set<ArtReviewRecord>().Add(new()
        {
            Id = Guid.NewGuid(),
            AccountId = Guid.NewGuid(),
            SceneKey = "day1-phone-0617",
            AssetKey = original.AssetKey,
            Version = 1,
            CompositionVersions = "{}",
            Critique = "The subject silhouette is obscured.",
            Correction = correction,
            Regenerate = true,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(ct);

        var revised = await SceneArtRevision.CreateAsync(catalog, db, original.AssetKey, ct);

        Assert.StartsWith("Required subject and composition correction: " + correction, revised.ResolvedPrompt);
        Assert.Contains(original.ResolvedPrompt, revised.ResolvedPrompt);
        Assert.Equal(original.NegativeInstructions, revised.NegativeInstructions);
        Assert.Equal(original.StyleBibleVersion, revised.StyleBibleVersion);
        Assert.Equal(original.PromptTemplateVersion, revised.PromptTemplateVersion);
        Assert.NotEqual(original.VisualFingerprint, revised.VisualFingerprint);
        Assert.Equal(revised, await SceneArtRevision.CreateAsync(catalog, db, original.AssetKey, ct));
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

    /// <summary>Selected authored actions prioritize bounded scene-art requests and reject unknown action identifiers.</summary>
    [Fact]
    public async Task Selected_authored_action_promotes_requested_scene_art()
    {
        await using var app = CreateApp(true);
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();
        using var response = await client.PostAsJsonAsync("/api/art/scene/resolve",
            new { keys = new[] { "bg-day1-bedroom-dark" }, selectedActionType = "help_injured_stranger" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(100, app.Services.GetRequiredService<Jobs>().LastRequest?.Priority);

        using var invalid = await client.PostAsJsonAsync("/api/art/scene/resolve",
            new { keys = new[] { "bg-day1-bedroom-dark" }, selectedActionType = "arbitrary" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
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
        await AddApprovedAsset(app, "bg-day1-bedroom-dark");
        using var delivered = await client.GetAsync("/api/art/bg-day1-bedroom-dark/1/png", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, delivered.StatusCode);
        Assert.Equal("image/png", delivered.Content.Headers.ContentType?.MediaType);
        Assert.Equal("\"png-hash\"", delivered.Headers.ETag?.Tag);
        Assert.Contains("public", delivered.Headers.CacheControl?.ToString());
        Assert.Equal(new byte[] { 1, 2, 3 }, await delivered.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        using var conditional = new HttpRequestMessage(HttpMethod.Get, "/api/art/bg-day1-bedroom-dark/1/png");
        conditional.Headers.IfNoneMatch.ParseAdd("\"png-hash\"");
        using var notModified = await client.SendAsync(conditional, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotModified, notModified.StatusCode);
        await AddApprovedAsset(app, "private-character", owner: Guid.NewGuid());
        using var privateArt = await client.GetAsync("/api/art/private-character/1/png", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, privateArt.StatusCode);
        using var wrongVersion = await client.GetAsync("/api/art/bg-day1-bedroom-dark/2/png", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, wrongVersion.StatusCode);
    }

    /// <summary>An approved scene asset with missing binaries is regenerated through the durable work pipeline.</summary>
    [Fact]
    public async Task Approved_scene_with_missing_binary_queues_replacement_art()
    {
        await using var app = CreateApp(true);
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();
        var key = "bg-day1-bedroom-dark";
        var assetId = await AddApprovedAsset(app, key);
        var catalog = app.Services.GetRequiredService<SceneArtCatalog>();
        var fingerprint = catalog.Create(key).VisualFingerprint;
        var storage = (Storage)app.Services.GetRequiredService<IArtBinaryStorage>();
        storage.MissingKeys.Add(key + "/master.png");
        app.Services.GetRequiredService<Assets>().Match = new(assetId, key, 1,
            key + "/master.png", key + "/image.webp", key + "/image.png");
        app.Services.GetRequiredService<Assets>().MatchFingerprint = fingerprint;

        var result = Assert.Single(await Resolve(client, key));

        Assert.Equal("pending", result.Status);
        Assert.NotNull(app.Services.GetRequiredService<Jobs>().LastRequest);
        Assert.NotEqual(fingerprint, app.Services.GetRequiredService<Jobs>().LastRequest!.VisualFingerprint);
    }

    /// <summary>Fingerprint reuse includes shared art and the requesting account's own private art only.</summary>
    [Fact]
    public async Task Fingerprint_lookup_scopes_private_art_to_its_owner()
    {
        await using var app = CreateApp(false);
        await app.StartAsync(TestContext.Current.CancellationToken);
        var fingerprint = Guid.NewGuid().ToString("N");
        var owner = Guid.NewGuid();
        await AddApprovedAsset(app, "shared", fingerprint: fingerprint);
        var privateId = await AddApprovedAsset(app, "private-owner", owner: owner, fingerprint: fingerprint);
        await AddApprovedAsset(app, "private-other", owner: Guid.NewGuid(), fingerprint: fingerprint);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FatewakeDbContext>();
        var assets = new ArtAssetStore(db);

        Assert.Equal("shared", (await assets.FindApprovedByFingerprintAsync(fingerprint, TestContext.Current.CancellationToken))?.Key);
        Assert.Equal(privateId, (await assets.FindApprovedByFingerprintAsync(fingerprint, TestContext.Current.CancellationToken, owner))?.Id);
        Assert.Equal("shared", (await assets.FindApprovedByFingerprintAsync(fingerprint, TestContext.Current.CancellationToken, Guid.NewGuid()))?.Key);
        Assert.Null(await assets.FindApprovedAsync("private-owner", 1, TestContext.Current.CancellationToken));
        Assert.Equal(privateId, (await assets.FindApprovedAsync("private-owner", 1, TestContext.Current.CancellationToken, owner))?.Id);
    }

    private static void WithCatalog(Action<string, SceneArtCatalog> assertions)
    {
        var root = Path.Combine(Path.GetTempPath(), "fatewake-style-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var source = Path.Combine(AppContext.BaseDirectory, "SceneArt");
            foreach (var file in Directory.EnumerateFiles(source, "*.md", SearchOption.AllDirectories))
            {
                var target = Path.Combine(root, Path.GetRelativePath(source, file));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target);
            }
            var catalog = new SceneArtCatalog(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ArtGeneration:CatalogRoot"] = root
            }).Build());
            assertions(Path.Combine(root, "STYLE-BIBLE.md"), catalog);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static async Task<Guid> AddApprovedAsset(WebApplication app, string key, int version = 1, Guid? owner = null, string? fingerprint = null)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FatewakeDbContext>();
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        db.ArtAssets.Add(new ArtAssetRecord
        {
            Id = id,
            Key = key,
            Version = version,
            AssetType = "test",
            Status = ArtAssetStatus.Approved,
            OwnerAccountId = owner,
            VisualFingerprint = fingerprint ?? Guid.NewGuid().ToString("N"),
            ContentHash = "master-hash",
            MasterPngStorageKey = key + "/master.png",
            Width = 1,
            Height = 1,
            HasAlpha = true,
            CreatedAt = now,
            ApprovedAt = now
        });
        db.ArtAssetDerivatives.AddRange(
            new ArtAssetDerivativeRecord
            {
                Id = Guid.NewGuid(),
                ArtAssetId = id,
                Kind = ArtDerivativeKind.WebP,
                StorageKey = key + "/image.webp",
                ContentType = "image/webp",
                ContentHash = "webp-hash",
                Width = 1,
                Height = 1,
                ByteSize = 3,
                CreatedAt = now
            },
            new ArtAssetDerivativeRecord
            {
                Id = Guid.NewGuid(),
                ArtAssetId = id,
                Kind = ArtDerivativeKind.OptimizedPng,
                StorageKey = key + "/image.png",
                ContentType = "image/png",
                ContentHash = "png-hash",
                Width = 1,
                Height = 1,
                ByteSize = 3,
                CreatedAt = now
            });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return id;
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
        public string? MatchFingerprint;
        public Task<ArtAssetMatch?> FindApprovedByFingerprintAsync(string fingerprint, CancellationToken ct = default, Guid? accountId = null)
            => Task.FromResult(MatchFingerprint is null || MatchFingerprint == fingerprint ? Match : null);
        public Task<ArtAssetMatch?> FindApprovedAsync(string key, int version, CancellationToken ct = default, Guid? accountId = null)
            => Task.FromResult(Match?.Key == key && Match.Version == version ? Match : null);
    }

    private sealed class Jobs(IServiceScopeFactory scopes) : IArtWorkJobFactory
    {
        public int Calls;
        public ArtWorkRequest? LastRequest;
        public async Task<CreatedWorkJob> CreateAsync(ArtWorkRequest request, CancellationToken ct = default)
        {
            Calls++;
            LastRequest = request;
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
        public HashSet<string> MissingKeys { get; } = new(StringComparer.Ordinal);
        public Task PutAsync(string key, Stream content, string type, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> ExistsAsync(string key, CancellationToken ct = default) => Task.FromResult(!MissingKeys.Contains(key));
        public Task<Stream> OpenReadAsync(string key, CancellationToken ct = default) => Task.FromResult<Stream>(new MemoryStream([1, 2, 3]));
    }
}
