using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Fatewake.Api;
using Fatewake.Infrastructure.Art;
using Fatewake.Infrastructure.Authentication;
using Fatewake.Infrastructure.Persistence;
using Fatewake.Infrastructure.Work;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fatewake.IntegrationTests;

/// <summary>Checks administrator provenance access, immutable prompt edits, and selected durable regeneration.</summary>
/// <see href="../../docs/code/tests/Fatewake.IntegrationTests/ArtReviewTests.md">Documentation</see>
public sealed class ArtReviewTests
{
    /// <summary>Save-only revisions preserve the serving binding; explicit regeneration pins a selected asset.</summary>
    [Fact]
    public async Task Admin_saves_prompt_only_then_explicitly_queues_pinned_replacement()
    {
        var connection = Environment.GetEnvironmentVariable("FATEWAKE_ART_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection))
            Assert.Skip("A disposable PostgreSQL artwork test database is required.");
        await using var app = await CreateAsync(connection);
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");
        var source = await app.Services.GetRequiredService<Fixture>().CreateAssetAsync();
        Guid currentRevision;
        int initialArtAssetJobs;
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FatewakeDbContext>();
            currentRevision = (await db.Set<ArtPromptRecord>().SingleAsync(row => row.AssetKey == source.Key,
                TestContext.Current.CancellationToken)).CurrentRevisionId!.Value;
            initialArtAssetJobs = await db.WorkJobs.CountAsync(row => row.JobType == "art.asset",
                TestContext.Current.CancellationToken);
        }

        using var provenanceResponse = await client.GetAsync(
            $"/api/art/reviews/prompt?assetKey={source.Key}&version={source.Version}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, provenanceResponse.StatusCode);
        var provenance = (await provenanceResponse.Content.ReadFromJsonAsync<ArtPromptReviewResponse>(
            TestContext.Current.CancellationToken))!;
        Assert.True(provenance.OriginalSubmissionAvailable);
        Assert.Equal("Exact source prompt used for this immutable image.", provenance.OriginalResolvedPrompt);
        Assert.Equal("Exact source negative.", provenance.OriginalNegativePrompt);
        Assert.Equal("test-image-provider", provenance.OriginalProvider);
        Assert.Equal("test-image-model", provenance.OriginalModel);
        Assert.Equal("seed", provenance.OriginalPromptSource);

        var noProvenanceAsset = await app.Services.GetRequiredService<Fixture>().CreateAssetAsync(withProvenance: false);
        using var missingProvenanceResponse = await client.GetAsync(
            $"/api/art/reviews/prompt?assetKey={noProvenanceAsset.Key}&version={noProvenanceAsset.Version}",
            TestContext.Current.CancellationToken);
        var missingProvenance = (await missingProvenanceResponse.Content.ReadFromJsonAsync<ArtPromptReviewResponse>(
            TestContext.Current.CancellationToken))!;
        Assert.False(missingProvenance.OriginalSubmissionAvailable);
        Assert.Null(missingProvenance.OriginalResolvedPrompt);

        var save = new ArtPromptReviewRequest("day1", source.Key, source.Version, provenance.LatestRevisionId!.Value,
            "An unchanged modest bedroom with a neatly rumpled bed.", "people, text", false);
        using var savedResponse = await client.PostAsJsonAsync("/api/art/reviews/prompt", save,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, savedResponse.StatusCode);
        var saved = (await savedResponse.Content.ReadFromJsonAsync<ArtPromptReviewSaveResult>(
            TestContext.Current.CancellationToken))!;
        Assert.True(saved.Saved);
        Assert.False(saved.RegenerationQueued);
        Assert.Contains("Enforced role: environment only", saved.PreviewResolvedPrompt);

        using (var seedScope = app.Services.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<FatewakeDbContext>();
            var importer = new ArtPromptSeedImporter(db, app.Services.GetRequiredService<SceneArtCatalog>(),
                seedScope.ServiceProvider.GetRequiredService<ILogger<ArtPromptSeedImporter>>());
            await importer.InitializeAsync(TestContext.Current.CancellationToken);
            var prompt = await db.Set<ArtPromptRecord>().AsNoTracking()
                .SingleAsync(row => row.AssetKey == source.Key, TestContext.Current.CancellationToken);
            var revision = await db.Set<ArtPromptRevisionRecord>().AsNoTracking()
                .SingleAsync(row => row.Id == saved.RevisionId, TestContext.Current.CancellationToken);
            Assert.Equal(saved.RevisionId, prompt.LatestRevisionId);
            Assert.Equal("An unchanged modest bedroom with a neatly rumpled bed.", revision.PositivePrompt);
        }

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FatewakeDbContext>();
            var prompt = await db.Set<ArtPromptRecord>().SingleAsync(row => row.AssetKey == source.Key,
                TestContext.Current.CancellationToken);
            Assert.Equal(currentRevision, prompt.CurrentRevisionId);
            Assert.Equal(saved.RevisionId, prompt.LatestRevisionId);
            Assert.Equal(0, await db.ArtRegenerationRequests.CountAsync(row => row.ArtAssetId == source.AssetId,
                TestContext.Current.CancellationToken));
            Assert.Equal(initialArtAssetJobs, await db.WorkJobs.CountAsync(row => row.JobType == "art.asset",
                TestContext.Current.CancellationToken));
        }

        var regenerate = save with
        {
            ExpectedRevisionId = saved.RevisionId,
            PositivePrompt = "An unchanged bedroom from a wider view.",
            Regenerate = true
        };
        using var queuedResponse = await client.PostAsJsonAsync("/api/art/reviews/prompt", regenerate,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, queuedResponse.StatusCode);
        var queued = (await queuedResponse.Content.ReadFromJsonAsync<ArtPromptReviewSaveResult>(
            TestContext.Current.CancellationToken))!;
        Assert.True(queued.RegenerationQueued);
        Assert.NotNull(queued.WorkJobId);
        using var staleResponse = await client.PostAsJsonAsync("/api/art/reviews/prompt", regenerate,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, staleResponse.StatusCode);

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FatewakeDbContext>();
            var prompt = await db.Set<ArtPromptRecord>().SingleAsync(row => row.AssetKey == source.Key,
                TestContext.Current.CancellationToken);
            Assert.Equal(currentRevision, prompt.CurrentRevisionId);
            Assert.Equal(queued.RevisionId, prompt.RequestedRevisionId);
            Assert.Equal(queued.WorkJobId, prompt.RequestedWorkJobId);
            Assert.Equal(1, await db.ArtRegenerationRequests.CountAsync(row => row.ArtAssetId == source.AssetId,
                TestContext.Current.CancellationToken));
            var regeneration = await db.ArtRegenerationRequests.SingleAsync(row => row.ArtAssetId == source.AssetId,
                TestContext.Current.CancellationToken);
            Assert.Equal(queued.WorkJobId, regeneration.RegenerationWorkJobId);
            Assert.Equal(source.AssetId, regeneration.ArtAssetId);
            var pinnedRequest = JsonSerializer.Deserialize<ArtWorkRequest>(regeneration.RequestJson);
            Assert.Equal(queued.RevisionId, pinnedRequest?.PromptRevisionId);
            Assert.True(pinnedRequest?.ForceProviderRegeneration);
            Assert.Equal(WorkJobStatus.Pending, (await db.WorkJobs.SingleAsync(row => row.Id == queued.WorkJobId,
                TestContext.Current.CancellationToken)).Status);
            Assert.Equal(ArtAssetStatus.Approved, (await db.ArtAssets.SingleAsync(row =>
                row.Id == source.AssetId, TestContext.Current.CancellationToken)).Status);
        }

        using var statusResponse = await client.GetAsync(
            $"/api/art/reviews/prompt?assetKey={source.Key}&version={source.Version}", TestContext.Current.CancellationToken);
        var status = (await statusResponse.Content.ReadFromJsonAsync<ArtPromptReviewResponse>(
            TestContext.Current.CancellationToken))!;
        Assert.Equal("pending", status.ReplacementStatus);
        Assert.Equal(queued.WorkJobId, status.ReplacementWorkJobId);
    }

    /// <summary>Complete-scene review queues each rendered layer with reusable visual guidance.</summary>
    [Fact]
    public async Task Scene_review_saves_preferences_and_atomically_queues_rendered_layers()
    {
        var connection = Environment.GetEnvironmentVariable("FATEWAKE_ART_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection))
            Assert.Skip("A disposable PostgreSQL artwork test database is required.");
        var ct = TestContext.Current.CancellationToken;
        await using var app = await CreateAsync(connection);
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");
        var fixture = app.Services.GetRequiredService<Fixture>();
        var background = await fixture.CreateAssetAsync(key: "bg-day1-house-hallway");
        var lighting = await fixture.CreateAssetAsync(key: "light-day1-power-flicker");
        var versions = new Dictionary<string, int?> { [background.Key] = background.Version, [lighting.Key] = lighting.Version };
        var preference = new ArtReviewRequest("day1-house-flicker", null, null, versions,
            "Keep silhouettes crisp and the palette restrained.", null, false, true);
        using var saved = await client.PostAsJsonAsync("/api/art/reviews", preference, ct);
        saved.EnsureSuccessStatusCode();

        using var queued = await client.PostAsJsonAsync("/api/art/reviews", preference with
        {
            Critique = "Use consistent dawn light across the composition.",
            Regenerate = true,
            UseForFutureArtwork = false
        }, ct);
        queued.EnsureSuccessStatusCode();
        var response = await queued.Content.ReadFromJsonAsync<JsonElement>(ct);
        var ids = response.GetProperty("jobIds").EnumerateArray().Select(value => value.GetGuid()).ToArray();
        Assert.Equal(2, ids.Length);
        using var duplicate = await client.PostAsJsonAsync("/api/art/reviews", preference with { Regenerate = true }, ct);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FatewakeDbContext>();
        var reviews = await db.Set<ArtReviewRecord>().Where(row => row.SceneKey == preference.SceneKey)
            .ToArrayAsync(ct);
        Assert.Equal(2, reviews.Length);
        Assert.Single(reviews, review => review.UseForFutureArtwork);
        var work = await db.WorkJobs.Where(job => ids.Contains(job.Id)).ToArrayAsync(ct);
        var requests = work.Select(job => JsonSerializer.Deserialize<ArtWorkRequest>(job.Payload)!).ToArray();
        Assert.Equal(versions.Keys.Order().ToArray(), requests.Select(request => request.AssetKey).Order().ToArray());
        Assert.All(requests, request =>
        {
            Assert.True(request.ForceProviderRegeneration);
            Assert.Contains("Keep silhouettes crisp", request.ResolvedPrompt);
            Assert.Contains("Use consistent dawn light", request.ResolvedPrompt);
        });
        var prompts = await db.Set<ArtPromptRecord>().Where(prompt => ids.Contains(prompt.RequestedWorkJobId!.Value)).ToArrayAsync(ct);
        Assert.Equal(2, prompts.Length);
        Assert.All(prompts, prompt => Assert.NotEqual(prompt.CurrentRevisionId, prompt.RequestedRevisionId));
    }

    /// <summary>Guests and non-admin accounts cannot read version-pinned prompt provenance.</summary>
    [Fact]
    public async Task Non_admin_cannot_read_prompt_provenance()
    {
        var connection = Environment.GetEnvironmentVariable("FATEWAKE_ART_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection))
            Assert.Skip("A disposable PostgreSQL artwork test database is required.");
        await using var app = await CreateAsync(connection);
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "User");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(
            "/api/art/reviews/prompt?assetKey=bg-day1-bedroom-dark&version=1",
            TestContext.Current.CancellationToken)).StatusCode);
    }

    private static async Task<WebApplication> CreateAsync(string connection)
    {
        var accountId = Guid.NewGuid();
        var userAccountId = Guid.NewGuid();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Administration:AdminEmails:0"] = "admin@example.invalid",
            ["ArtGeneration:Provider"] = "Local",
            ["ArtGeneration:Local:Enabled"] = "true"
        });
        builder.Services.AddDbContext<FatewakeDbContext>(options => options.UseNpgsql(connection));
        builder.Services.AddAuthentication("test").AddScheme<AuthenticationSchemeOptions, TestAuthentication>(
            "test", _ => { });
        builder.Services.AddArtReviewAuthorization(builder.Configuration);
        builder.Services.AddSingleton<SceneArtCatalog>();
        builder.Services.AddScoped<IWorkJobBuilder, WorkJobBuilder>();
        builder.Services.AddScoped<IArtWorkJobFactory, ArtWorkJobFactory>();
        builder.Services.AddSingleton<IWorkSignalBus, NullWorkSignalBus>();
        builder.Services.AddSingleton<Fixture>();
        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapArtReviews();
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FatewakeDbContext>();
        await DatabaseInitializer.InitializeAsync(db, TestContext.Current.CancellationToken);
        await new ArtPromptSeedImporter(db, app.Services.GetRequiredService<SceneArtCatalog>(),
            scope.ServiceProvider.GetRequiredService<ILogger<ArtPromptSeedImporter>>())
            .InitializeAsync(TestContext.Current.CancellationToken);
        db.Accounts.AddRange(new AccountRecord
        {
            Id = accountId,
            PrimaryEmail = "admin@example.invalid",
            Status = AccountStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        }, new AccountRecord
        {
            Id = userAccountId,
            PrimaryEmail = "user@example.invalid",
            Status = AccountStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        db.Set<AccountRoleRecord>().AddRange(
            new AccountRoleRecord { AccountId = accountId, Role = "Admin", AssignedAt = DateTimeOffset.UtcNow },
            new AccountRoleRecord { AccountId = userAccountId, Role = "User", AssignedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        app.Services.GetRequiredService<Fixture>().AccountId = accountId;
        app.Services.GetRequiredService<Fixture>().UserAccountId = userAccountId;
        return app;
    }

    private sealed class Fixture(IServiceScopeFactory scopes)
    {
        public Guid AccountId
        {
            get; set;
        }
        public Guid UserAccountId
        {
            get; set;
        }

        public async Task<(Guid AssetId, string Key, int Version)> CreateAssetAsync(bool withProvenance = true,
            string key = "bg-day1-bedroom-dark")
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FatewakeDbContext>();
            var version = (await db.ArtAssets.Where(row => row.Key == key).MaxAsync(row => (int?)row.Version,
                TestContext.Current.CancellationToken) ?? 0) + 1;
            var id = Guid.NewGuid();
            var now = DateTimeOffset.UtcNow;
            db.ArtAssets.Add(new ArtAssetRecord
            {
                Id = id,
                Key = key,
                Version = version,
                AssetType = "Background",
                Status = ArtAssetStatus.Approved,
                VisualFingerprint = Guid.NewGuid().ToString("N"),
                ContentHash = Guid.NewGuid().ToString("N"),
                MasterPngStorageKey = $"test/{id:N}/master.png",
                Width = 640,
                Height = 360,
                HasAlpha = false,
                OwnerAccountId = null,
                CreatedAt = now,
                ApprovedAt = now
            });
            if (withProvenance)
            {
                var revisionId = await db.Set<ArtPromptRecord>().Where(row => row.AssetKey == key)
                    .Select(row => row.CurrentRevisionId).SingleAsync(TestContext.Current.CancellationToken);
                var workJobId = Guid.NewGuid();
                db.WorkJobs.Add(new WorkJobRecord
                {
                    Id = workJobId,
                    JobType = "art.asset",
                    IdempotencyKey = $"test-source:{id:N}",
                    Status = WorkJobStatus.Completed,
                    Payload = "{}",
                    CreatedAt = now,
                    UpdatedAt = now,
                    CompletedAt = now
                });
                db.ArtGenerations.Add(new ArtGenerationRecord
                {
                    Id = Guid.NewGuid(),
                    WorkJobId = workJobId,
                    ArtAssetId = id,
                    PromptRevisionId = revisionId,
                    IdempotencyKey = $"test-source:{id:N}",
                    Operation = "generate",
                    PromptKey = key,
                    PromptTemplateVersion = "1",
                    ResolvedPrompt = "Exact source prompt used for this immutable image.",
                    NegativePrompt = "Exact source negative.",
                    Provider = "test-image-provider",
                    Model = "test-image-model",
                    ProviderJobId = "provider-job-test",
                    VisualFingerprint = Guid.NewGuid().ToString("N"),
                    CreatedAt = now,
                    CompletedAt = now
                });
            }
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            return (id, key, version);
        }
    }

    private sealed class TestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role = Request.Headers["X-Test-Role"].ToString();
            if (role.Length == 0)
                return Task.FromResult(AuthenticateResult.NoResult());
            var fixture = Context.RequestServices.GetRequiredService<Fixture>();
            var accountId = role == "Admin" ? fixture.AccountId : fixture.UserAccountId;
            var user = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, accountId.ToString()),
                new Claim(ClaimTypes.Role, role)
            ], "test"));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(user, "test")));
        }
    }
}
