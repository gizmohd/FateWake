using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Fatewake.Api;
using Fatewake.Infrastructure.Art;
using Fatewake.Infrastructure.Authentication;
using Fatewake.Infrastructure.Persistence;
using Fatewake.Infrastructure.Usage;
using Fatewake.Observability;
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
/// <summary>Verifies real PostgreSQL aggregates, role boundaries and nested request drill-down.</summary>
public sealed class AdminDashboardTests
{
    /// <summary>Attempts remain separate while repeated provider executions are accounted once.</summary>
    [Fact]
    public async Task Dashboard_is_admin_only_and_reports_deduplicated_usage_with_calendar_and_user_filters()
    {
        var connection = Environment.GetEnvironmentVariable("FATEWAKE_ADMIN_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) Assert.Skip("An isolated admin dashboard database is required.");
        var ct = TestContext.Current.CancellationToken;
        var options = new DbContextOptionsBuilder<FatewakeDbContext>().UseNpgsql(connection).Options;
        var admin = Guid.NewGuid(); var user = Guid.NewGuid(); var jobs = Enumerable.Range(0, 6).Select(_ => Guid.NewGuid()).ToArray();
        await using (var db = new FatewakeDbContext(options))
        {
            await db.Database.MigrateAsync(ct);
            db.Accounts.AddRange(new AccountRecord { Id = admin, PrimaryEmail = "alpha@test.example", Status = AccountStatus.Active }, new AccountRecord { Id = user, PrimaryEmail = "bravo@test.example", Status = AccountStatus.Active });
            if (Environment.GetEnvironmentVariable("FATEWAKE_ADMIN_UI_FIXTURE") == "1")
            {
                var credential = new LocalCredentialRecord { AccountId = admin, NormalizedEmail = "ALPHA@TEST.EXAMPLE", PasswordHash = "pending" };
                credential.PasswordHash = new Microsoft.AspNetCore.Identity.PasswordHasher<LocalCredentialRecord>().HashPassword(credential, "Admin fixture testing only 123!");
                db.LocalCredentials.Add(credential); db.AccountEmails.Add(new AccountEmailRecord { AccountId = admin, NormalizedEmail = credential.NormalizedEmail, Verified = true });
            }
            db.AddRange(new AccountRoleRecord { AccountId = admin, Role = "Admin", AssignedAt = DateTimeOffset.UtcNow }, new AccountRoleRecord { AccountId = user, Role = "User", AssignedAt = DateTimeOffset.UtcNow });
            var states = new[] { WorkJobStatus.Completed, WorkJobStatus.Failed, WorkJobStatus.Pending, WorkJobStatus.Running, WorkJobStatus.Cancelled, WorkJobStatus.Completed };
            for (var i = 0; i < jobs.Length; i++)
            {
                var request = new ArtWorkRequest("bg-day1-bedroom-dark", "Background", "fp" + i, "PRIVATE PROMPT", i == 0 ? "OpenAI" : i == 1 ? "Local" : "ComfyUI", "test-model", AccountId: i == 0 ? admin : i == 1 ? user : null);
                db.WorkJobs.Add(new WorkJobRecord { Id = jobs[i], JobType = "art.asset", IdempotencyKey = "job" + i, Status = states[i], Payload = JsonSerializer.Serialize(request), CreatedAt = DateTimeOffset.Parse("2025-12-31T22:00:00Z") });
                db.WorkSteps.Add(new WorkStepRecord { Id = Guid.NewGuid(), JobId = jobs[i], StepType = ArtWorkStepTypes.GenerateMaster, Queue = "art", Status = i == 1 ? WorkStepStatus.Failed : WorkStepStatus.Completed, AttemptCount = i == 0 ? 2 : 1, ErrorDetail = "PRIVATE SECRET FAILURE", ErrorCode = i == 1 ? "execution_failed" : null });
            }
            for (var version = 1; version <= 2; version++) db.ArtAssets.Add(new ArtAssetRecord { Id = Guid.NewGuid(), Key = "bg-day1-bedroom-dark", Version = version, AssetType = "Background", Status = ArtAssetStatus.Approved, VisualFingerprint = "fp0", ContentHash = "hash" + version, MasterPngStorageKey = "master", CreatedAt = DateTimeOffset.Parse("2025-12-31T23:00:00Z") });
            db.WorkArtifacts.Add(new WorkArtifactRecord { Id = Guid.NewGuid(), JobId = jobs[5], Key = "art.master", Value = "{\"Reused\":true}" });
            await db.SaveChangesAsync(ct);
            var recorder = new AiUsageRecorder(db);
            var first = await recorder.BeginAsync(new("OpenAI", "test-model", "image.generate", "attempt-1", jobs[0], admin), ct);
            Assert.Equal(first, await recorder.BeginAsync(new("OpenAI", "test-model", "image.generate", "attempt-1", jobs[0], admin), ct));
            await recorder.ReportAsync(first, new("provider-id", 100, 50, 150, 10, 80, 20, 50, 0.5m, 2m, 0.2m, "USD"), ct);
            await recorder.ReportAsync(first, new("provider-id", 100, 50, 150, 10), ct);
            await recorder.FinishAsync(first, "succeeded", ct);
            var duplicate = await recorder.BeginAsync(new("OpenAI", "test-model", "image.generate", "attempt-2", jobs[0], admin), ct);
            await recorder.ReportAsync(duplicate, new("provider-id", 100, 50, 150, Cost: 0.2m, Currency: "USD"), ct);
            await recorder.FinishAsync(duplicate, "failed", ct);
            var local = await recorder.BeginAsync(new("Local", "test-model", "image.generate", "attempt-3", jobs[1], user), ct);
            await recorder.ReportAsync(local, new(InputTokens: 10, OutputTokens: 0, TotalTokens: 10, Cost: 2m, Currency: "EUR"), ct);
            await recorder.FinishAsync(local, "failed", ct);
            await recorder.BeginAsync(new("ComfyUI", "test-model", "image.generate", "attempt-4", jobs[2]), ct);
            var rows = await db.Set<AiUsageRecord>().OrderBy(x => x.AttemptKey).ToArrayAsync(ct);
            for (var i = 0; i < rows.Length; i++) rows[i].StartedAt = DateTimeOffset.Parse(i < 2 ? "2026-01-01T02:00:00Z" : "2026-01-01T06:00:00Z");
            await db.SaveChangesAsync(ct);
        }
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer();
        builder.Services.AddDbContext<FatewakeDbContext>(x => x.UseNpgsql(connection));
        builder.Services.AddAuthentication("test").AddScheme<AuthenticationSchemeOptions, TestIdentity>("test", _ => { });
        builder.Services.AddArtReviewAuthorization(new ConfigurationBuilder().Build());
        builder.Services.AddScoped<AdminDashboardService>();
        await using var app = builder.Build(); app.UseAuthentication(); app.UseAuthorization(); app.MapAdminDashboard(); await app.StartAsync(ct);
        using var client = app.GetTestClient();
        var routes = new[] { "/summary?range=all", "/jobs?range=all", "/usage?range=all", "/usage/groups?range=all", "/users", "/jobs/" + jobs[0] };
        foreach (var route in routes) Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin" + route, ct)).StatusCode);
        client.DefaultRequestHeaders.Add("TestAccount", user.ToString());
        foreach (var route in routes) Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin" + route, ct)).StatusCode);
        client.DefaultRequestHeaders.Remove("TestAccount"); client.DefaultRequestHeaders.Add("TestAccount", admin.ToString());
        var summary = (await client.GetFromJsonAsync<AdminDashboardSummary>("/api/admin/summary?range=all", ct))!;
        Assert.Equal(4, summary.Usage.Calls); Assert.Equal(1, summary.Usage.DuplicateReports); Assert.Equal(160, summary.Usage.TotalTokens);
        Assert.Equal(10, summary.Usage.CachedTokens); Assert.Equal(0.5m, summary.Usage.Credits); Assert.Equal(2, summary.Costs.Length);
        Assert.Equal(0.2m, summary.Costs.Single(x => x.Currency == "USD").Amount); Assert.Equal(2m, summary.Costs.Single(x => x.Currency == "EUR").Amount);
        Assert.Equal(2, summary.ImageVersions); Assert.Equal(1, summary.DistinctAssets); Assert.Equal(1, summary.ReusedJobs);
        var years = (await client.GetFromJsonAsync<AiCalendarGroup[]>("/api/admin/usage/groups?range=all", ct))!;
        Assert.Equal(new[] { "2026", "2025" }, years.Select(x => x.Key)); Assert.All(years, x => Assert.Equal(2, x.Metrics.Calls));
        var months = (await client.GetFromJsonAsync<AiCalendarGroup[]>("/api/admin/usage/groups?range=all&level=month&parent=2025", ct))!;
        Assert.Equal("2025-12", Assert.Single(months).Key);
        var days = (await client.GetFromJsonAsync<AiCalendarGroup[]>("/api/admin/usage/groups?range=all&level=day&parent=2025-12", ct))!;
        Assert.Equal("2025-12-31", Assert.Single(days).Key);
        var requests = (await client.GetFromJsonAsync<AdminResultPage<AdminUsageRow>>("/api/admin/usage?range=all&level=request&parent=2025-12-31&pageSize=1&sort=user&descending=false", ct))!;
        Assert.Equal(2, requests.Total); Assert.Single(requests.Items); Assert.Equal(admin, requests.Items[0].AccountId);
        var filtered = (await client.GetFromJsonAsync<AdminDashboardSummary>($"/api/admin/summary?range=all&accountId={user}", ct))!;
        Assert.Equal(1, filtered.Usage.Calls); Assert.Equal(10, filtered.Usage.TotalTokens);
        var page = (await client.GetFromJsonAsync<AdminResultPage<AdminJobRow>>("/api/admin/jobs?range=all&sort=user&descending=false", ct))!;
        Assert.Equal(6, page.Total); Assert.Contains(page.Items, x => x.AssetVersion == 2);
        var detail = await client.GetStringAsync("/api/admin/jobs/" + jobs[1], ct);
        Assert.DoesNotContain("PRIVATE", detail); Assert.Contains("Processing failed", detail);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/admin/summary?pageSize=101", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/admin/usage/groups?level=month", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/admin/summary", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/admin/usage/groups?range=all&sort=tokens", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/admin/usage?range=all&sort=tokens", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/admin/jobs?range=all&sort=duration", ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/admin/summary?range=all&operation=image.generate&provider=OpenAI", ct)).StatusCode);
        await using (var db = new FatewakeDbContext(options)) Assert.Equal(4, await db.Set<AiUsageRecord>().CountAsync(ct));
    }
    private sealed class TestIdentity(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logs, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logs, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("TestAccount", out var account)) return Task.FromResult(AuthenticateResult.NoResult());
            // A forged incoming Admin claim must be replaced by the persisted canonical role.
            var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, account.ToString()), new Claim(ClaimTypes.Role, "Admin")], "test"));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(user, "test")));
        }
    }
}
