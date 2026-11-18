using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
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
/// <summary>Checks artwork critique permissions, durable provenance, and replacement cache identity.</summary>
public sealed class ArtReviewTests
{
    private static readonly Guid AccountId = Guid.NewGuid();
    /// <summary>Review routes reject guests and ordinary authenticated accounts.</summary>
    [Theory]
    [InlineData("", HttpStatusCode.Unauthorized)]
    [InlineData("Player", HttpStatusCode.Forbidden)]
    public async Task Only_administrators_can_read_or_write_reviews(string role, HttpStatusCode expected)
    {
        await using var app = await CreateAsync(false);
        using var client = app.GetTestClient();
        if (role.Length > 0) client.DefaultRequestHeaders.Add("X-Test-Role", role);
        Assert.Equal(expected, (await client.GetAsync("/api/art/reviews?sceneKey=day1-phone-0617", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(expected, (await client.PostAsJsonAsync("/api/art/reviews", Request(false), TestContext.Current.CancellationToken)).StatusCode);
    }
    /// <summary>Saves exact reviewed versions and queues an independently addressed corrected layer.</summary>
    [Fact]
    public async Task Admin_correction_is_persisted_and_changes_generation_identity()
    {
        await using var app = await CreateAsync();
        using var client = app.GetTestClient(); client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");
        var response = await client.PostAsJsonAsync("/api/art/reviews", Request(true), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<FatewakeDbContext>();
        var review = await db.Set<ArtReviewRecord>().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(AccountId, review.AccountId); Assert.Equal(1, review.Version);
        Assert.Contains("bg-day1-bedroom-dark", review.CompositionVersions);
        var original = app.Services.GetRequiredService<SceneArtCatalog>().Create("bg-day1-bedroom-dark");
        var revised = await SceneArtRevision.CreateAsync(app.Services.GetRequiredService<SceneArtCatalog>(), db, original.AssetKey, TestContext.Current.CancellationToken);
        Assert.NotEqual(original.VisualFingerprint, revised.VisualFingerprint);
        Assert.StartsWith("Required subject and composition correction: Empty bedroom", revised.ResolvedPrompt);
        Assert.Equal(revised.VisualFingerprint, app.Services.GetRequiredService<Jobs>().Request!.VisualFingerprint);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/art/reviews", Request(false) with { Version = 42 }, TestContext.Current.CancellationToken)).StatusCode);
        Assert.Single(await db.Set<ArtReviewRecord>().ToArrayAsync(TestContext.Current.CancellationToken));
    }
    /// <summary>Only persisted roles grant Admin; a token claim cannot promote an ordinary account.</summary>
    [Fact]
    public async Task Token_Admin_claim_is_replaced_by_canonical_User_role()
    {
        await using var app = await CreateAsync(false); using var scope = app.Services.CreateScope();
        var claims = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, AccountId.ToString()), new Claim(ClaimTypes.Role, "Admin")], "test"));
        var transformer = scope.ServiceProvider.GetRequiredService<IClaimsTransformation>();
        var resolved = await transformer.TransformAsync(claims);
        Assert.False(resolved.IsInRole("Admin")); Assert.True(resolved.IsInRole("User"));
    }
    private static ArtReviewRequest Request(bool regenerate) => new("day1-phone-0617", "bg-day1-bedroom-dark", 1,
        new() { ["bg-day1-bedroom-dark"] = 1 }, "Background incorrectly contains a person.", "Empty bedroom, environment only.", regenerate);
    private static async Task<WebApplication> CreateAsync(bool admin = true)
    {
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string,string?> { ["Administration:AdminEmails:0"]="gizmohd@gmail.com",
            ["ArtGeneration:Provider"]="Local", ["ArtGeneration:Local:Enabled"]="true" });
        var database = Guid.NewGuid().ToString(); builder.Services.AddDbContext<FatewakeDbContext>(o => o.UseInMemoryDatabase(database));
        builder.Services.AddAuthentication("test").AddScheme<AuthenticationSchemeOptions, TestAuthentication>("test", _ => { });
        builder.Services.AddArtReviewAuthorization(builder.Configuration); builder.Services.AddSingleton<SceneArtCatalog>();
        builder.Services.AddSingleton<Jobs>(); builder.Services.AddSingleton<IArtWorkJobFactory>(s=>s.GetRequiredService<Jobs>());
        var app = builder.Build(); app.UseAuthentication(); app.UseAuthorization(); app.MapArtReviews();
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var scope=app.Services.CreateScope(); var db=scope.ServiceProvider.GetRequiredService<FatewakeDbContext>();
        db.Add(new AccountRoleRecord { AccountId = AccountId, Role = admin ? "Admin" : "User", AssignedAt = DateTimeOffset.UtcNow });
        db.ArtAssets.Add(new ArtAssetRecord { Id=Guid.NewGuid(), Key="bg-day1-bedroom-dark", Version=1, AssetType="Background",
            Status=ArtAssetStatus.Approved, VisualFingerprint="old", ContentHash="hash", MasterPngStorageKey="image.png" });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken); return app;
    }
    private sealed class Jobs : IArtWorkJobFactory
    {
        public ArtWorkRequest? Request;
        public Task<CreatedWorkJob> CreateAsync(ArtWorkRequest request, CancellationToken ct=default)
        { Request=request; return Task.FromResult(new CreatedWorkJob(Guid.NewGuid(),false,new Dictionary<string,Guid>())); }
    }
    private sealed class TestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role=Request.Headers["X-Test-Role"].ToString();
            if (role.Length==0) return Task.FromResult(AuthenticateResult.NoResult());
            var user=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,AccountId.ToString()),new Claim(ClaimTypes.Role,role)],"test"));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(user,"test")));
        }
    }
}


