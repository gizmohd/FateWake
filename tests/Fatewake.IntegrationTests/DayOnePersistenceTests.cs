using System.Text.Json;
using Fatewake.GameEngine;
using Fatewake.GameEngine.DayOne;
using Fatewake.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Fatewake.Infrastructure.Art;
using Fatewake.Infrastructure.Work;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Fatewake.Infrastructure.Authentication;
using Fatewake.Api.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Fatewake.Web.Authentication;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace Fatewake.IntegrationTests;

/// <summary>Verifies PostgreSQL persistence, migrations, and atomic day-one resolution behavior.</summary>
/// <see href="../../docs/code/tests/Fatewake.IntegrationTests/DayOnePersistenceTests.md">DayOnePersistenceTests documentation</see>
public sealed class DayOnePersistenceTests
{
    /// <summary>The email migration preserves older local accounts and requires proof before login or external linking.</summary>
    [Fact]
    public async Task Email_migration_preserves_legacy_accounts_and_requires_verification()
    {
        var cs = Environment.GetEnvironmentVariable("FATEWAKE_TEST_CONNECTION");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(cs), "Requires an isolated FATEWAKE_TEST_CONNECTION database.");
        var ct = TestContext.Current.CancellationToken;
        await using var db = new FatewakeDbContext(new DbContextOptionsBuilder<FatewakeDbContext>().UseNpgsql(cs).Options);
        await db.Database.EnsureDeletedAsync(ct);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261006000400_LocalCredentials", ct);
        var accountId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var account = new AccountRecord { Id = accountId, PrimaryEmail = "legacy@example.com", Status = AccountStatus.Active, CreatedAt = now, UpdatedAt = now };
        var hasher = new PasswordHasher<LocalCredentialRecord>();
        const string password = "my legacy account password";
        var credential = new LocalCredentialRecord { AccountId = accountId, NormalizedEmail = "LEGACY@EXAMPLE.COM", PasswordHash = "" };
        credential.PasswordHash = hasher.HashPassword(credential, password);
        var oldHash = credential.PasswordHash;
        db.Accounts.Add(account); db.LocalCredentials.Add(credential);
        await db.SaveChangesAsync(ct);
        await DatabaseInitializer.InitializeAsync(db, ct);
        Assert.False((await db.AccountEmails.SingleAsync(ct)).Verified);
        using var mailbox = new TestEmailMailbox();
        var local = mailbox.Accounts(db, hasher);
        Assert.True((await local.LoginAsync(account.PrimaryEmail, password, ct)).VerificationRequired);
        await mailbox.Verification(db).ResendAsync(account.PrimaryEmail, ct);
        Assert.Equal(accountId, (await mailbox.Verification(db).VerifyAsync(mailbox.Token(account.PrimaryEmail), ct)).AccountId);
        Assert.Equal(accountId, (await local.LoginAsync(account.PrimaryEmail, password, ct)).AccountId);
        Assert.Equal(oldHash, credential.PasswordHash);
        await migrator.MigrateAsync("20261006000400_LocalCredentials", ct);
        Assert.Equal(accountId, (await db.Accounts.SingleAsync(ct)).Id);
        Assert.Equal(oldHash, (await db.LocalCredentials.SingleAsync(ct)).PasswordHash);
    }

    /// <summary>OIDC code callbacks validate nonce/PKCE, exchange provider identity, and prompt for a first local password.</summary>
    [Theory]
    [InlineData("Google")]
    [InlineData("Microsoft")]
    public async Task External_oidc_callback_creates_canonical_session_and_prompts_for_password(string provider)
    {
        var cs = Environment.GetEnvironmentVariable("FATEWAKE_TEST_CONNECTION");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(cs), "Requires an isolated FATEWAKE_TEST_CONNECTION database.");
        var ct = TestContext.Current.CancellationToken;
        await using var db = new FatewakeDbContext(new DbContextOptionsBuilder<FatewakeDbContext>().UseNpgsql(cs).Options);
        await db.Database.EnsureDeletedAsync(ct);
        using var mailbox = new TestEmailMailbox();
        using var rsa = RSA.Create(2048);
        var key = new RsaSecurityKey(rsa) { KeyId = "oidc-test" };
        var metadata = new OpenIdConnectConfiguration
        {
            Issuer = "https://provider.test", AuthorizationEndpoint = "https://provider.test/authorize",
            TokenEndpoint = "https://provider.test/token"
        };
        metadata.SigningKeys.Add(key);
        await using var apiFactory = new WebApplicationFactory<LocalLoginRequest>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development").UseSetting("ConnectionStrings:fatewake", cs)
                .UseSetting("Email:Directory", mailbox.DirectoryPath)
                .UseSetting($"Authentication:{provider}:Enabled", "true").UseSetting($"Authentication:{provider}:ClientId", "test-client");
            builder.ConfigureServices(services => services.PostConfigure<JwtBearerOptions>(provider,
                options => options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(metadata)));
        });
        using var apiClient = apiFactory.CreateClient();
        using var backchannel = new TestOidcBackchannel(key);
        Exception? providerFailure = null;
        await using var webFactory = new WebApplicationFactory<LocalAccountInfo>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development").UseSetting($"Authentication:{provider}:Enabled", "true")
                .UseSetting($"Authentication:{provider}:ClientId", "test-client").UseSetting($"Authentication:{provider}:ClientSecret", "test-secret");
            builder.ConfigureServices(services =>
            {
                services.AddHttpClient("fatewake-api", client => client.BaseAddress = new("http://localhost"))
                    .ConfigurePrimaryHttpMessageHandler(() => apiFactory.Server.CreateHandler());
                services.PostConfigure<OpenIdConnectOptions>(provider, options =>
                {
                    options.Configuration = metadata;
                    options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(metadata);
                    options.Backchannel = new HttpClient(backchannel);
                    var failureHandler = options.Events.OnRemoteFailure;
                    options.Events.OnRemoteFailure = context =>
                    {
                        providerFailure = context.Failure;
                        return failureHandler(context);
                    };
                });
            });
        });
        using var browser = webFactory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new("https://localhost") });
        var html = await browser.GetStringAsync("/login", ct);
        Assert.Contains("Continue with " + provider, html);
        var antiforgery = WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        using var challenged = await browser.PostAsync("/auth/external/" + provider.ToLowerInvariant(), new FormUrlEncodedContent(new Dictionary<string, string>
        { ["__RequestVerificationToken"] = antiforgery }), ct);
        Assert.Equal(HttpStatusCode.Redirect, challenged.StatusCode);
        var query = challenged.Headers.Location!.Query.TrimStart('?').Split('&').Select(x => x.Split('=', 2))
            .ToDictionary(x => x[0], x => Uri.UnescapeDataString(x[1]));
        Assert.Equal("code", query["response_type"]);
        Assert.Equal("S256", query["code_challenge_method"]);
        backchannel.Nonce = query["nonce"];
        backchannel.CodeChallenge = query["code_challenge"];
        using var callback = await browser.PostAsync("/signin-" + provider.ToLowerInvariant(), new FormUrlEncodedContent(new Dictionary<string, string>
        { ["state"] = query["state"], ["code"] = "test-code" }), ct);
        Assert.True(callback.Headers.Location?.OriginalString == (provider == "Google" ? "/set-password" : "/check-email"),
            providerFailure?.ToString() ?? callback.Headers.Location?.OriginalString);
        if (provider == "Microsoft")
        {
            Assert.Empty(await db.Accounts.ToListAsync(ct));
            var token = mailbox.Token("oidc@example.com");
            var proofHtml = await browser.GetStringAsync("/verify-email?token=" + token, ct);
            var proofAntiforgery = WebUtility.HtmlDecode(Regex.Match(proofHtml, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
            using var proof = await browser.PostAsync("/auth/verify-email", new FormUrlEncodedContent(new Dictionary<string, string>
            { ["__RequestVerificationToken"] = proofAntiforgery, ["token"] = token }), ct);
            Assert.Equal("/set-password", proof.Headers.Location?.OriginalString);
        }
        Assert.Contains("oidc@example.com", await browser.GetStringAsync("/account", ct));
        Assert.Single(await db.Accounts.ToListAsync(ct));
        Assert.Single(await db.ExternalIdentities.ToListAsync(ct));
        Assert.Empty(await db.LocalCredentials.ToListAsync(ct));
        Assert.True((await db.AccountEmails.SingleAsync(ct)).Verified);
    }

    /// <summary>Browser forms verify emailed links, prompt external users for a backup password, and reject missing antiforgery proofs.</summary>
    [Fact]
    public async Task Browser_verification_and_password_setup_are_antiforgery_protected()
    {
        var cs = Environment.GetEnvironmentVariable("FATEWAKE_TEST_CONNECTION");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(cs), "Requires an isolated FATEWAKE_TEST_CONNECTION database.");
        var ct = TestContext.Current.CancellationToken;
        await using var db = new FatewakeDbContext(new DbContextOptionsBuilder<FatewakeDbContext>().UseNpgsql(cs).Options);
        await db.Database.EnsureDeletedAsync(ct);
        using var mailbox = new TestEmailMailbox();
        await using var apiFactory = new WebApplicationFactory<LocalLoginRequest>().WithWebHostBuilder(builder =>
            builder.UseEnvironment("Development").UseSetting("ConnectionStrings:fatewake", cs)
                .UseSetting("Email:Directory", mailbox.DirectoryPath));
        using var apiClient = apiFactory.CreateClient();
        await using var webFactory = new WebApplicationFactory<LocalAccountInfo>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureServices(services => services.AddHttpClient("fatewake-api", client => client.BaseAddress = new("http://localhost"))
                .ConfigurePrimaryHttpMessageHandler(() => apiFactory.Server.CreateHandler()));
        });
        using var browser = webFactory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        async Task<HttpResponseMessage> Form(string page, string action, Dictionary<string, string> values)
        {
            using var document = await browser.GetAsync(page, ct);
            Assert.Equal(HttpStatusCode.OK, document.StatusCode);
            var html = await document.Content.ReadAsStringAsync(ct);
            var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
            Assert.True(match.Success);
            values["__RequestVerificationToken"] = WebUtility.HtmlDecode(match.Groups[1].Value);
            return await browser.PostAsync(action, new FormUrlEncodedContent(values), ct);
        }
        using var missingCsrf = await browser.PostAsync("/auth/register",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["email"] = "web@example.com", ["password"] = "a sufficiently long password" }), ct);
        Assert.Equal(HttpStatusCode.BadRequest, missingCsrf.StatusCode);
        const string password = "a sufficiently long password";
        using var registration = await Form("/register", "/auth/register", new()
        {
            ["email"] = "web@example.com", ["password"] = password, ["confirmPassword"] = password
        });
        Assert.Equal("/check-email", registration.Headers.Location?.OriginalString);
        var token = mailbox.Token("web@example.com");
        using var page = await browser.GetAsync("/verify-email?token=" + token, ct);
        Assert.True(page.Headers.CacheControl?.NoStore);
        Assert.Equal("no-referrer", Assert.Single(page.Headers.GetValues("Referrer-Policy")));
        using var confirmed = await Form("/verify-email?token=" + token, "/auth/verify-email", new() { ["token"] = token, ["password"] = password });
        Assert.Equal("/", confirmed.Headers.Location?.OriginalString);
        Assert.Contains(confirmed.Headers.GetValues("Set-Cookie"), x => x.StartsWith("fatewake.auth=") && x.Contains("httponly", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("web@example.com", await browser.GetStringAsync("/account", ct));
        using var logout = await Form("/account", "/auth/logout", new());
        Assert.Equal("/", logout.Headers.Location?.OriginalString);
        Assert.DoesNotContain("web@example.com", await browser.GetStringAsync("/account", ct));
        var verification = mailbox.Verification(db);
        var external = await verification.ExternalAsync(new("Microsoft", "web-external", "external-web@example.com", false, null, null), ct);
        Assert.True(external.VerificationRequired);
        var externalToken = mailbox.Token("external-web@example.com");
        using var verifiedExternal = await Form("/verify-email?token=" + externalToken, "/auth/verify-email", new() { ["token"] = externalToken });
        Assert.Equal("/set-password", verifiedExternal.Headers.Location?.OriginalString);
        using var missingPasswordCsrf = await browser.PostAsync("/auth/set-password", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["password"] = password, ["confirmPassword"] = password }), ct);
        Assert.Equal(HttpStatusCode.BadRequest, missingPasswordCsrf.StatusCode);
        using var configured = await Form("/set-password", "/auth/set-password", new() { ["password"] = password, ["confirmPassword"] = password });
        Assert.Equal("/account?password=added", configured.Headers.Location?.OriginalString);
        using var secondLogout = await Form("/account", "/auth/logout", new());
        using var localLogin = await Form("/login", "/auth/login", new() { ["email"] = "external-web@example.com", ["password"] = password });
        Assert.Equal("/", localLogin.Headers.Location?.OriginalString);
        Assert.Contains("external-web@example.com", await browser.GetStringAsync("/account", ct));
    }

    /// <summary>Provider exchange verifies cryptographic signature, issuer, audience and expiry, then prompts password setup on new accounts.</summary>
    [Fact]
    public async Task External_token_exchange_validates_tokens_and_supports_local_password_setup()
    {
        var cs = Environment.GetEnvironmentVariable("FATEWAKE_TEST_CONNECTION");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(cs), "Requires an isolated FATEWAKE_TEST_CONNECTION database.");
        var ct = TestContext.Current.CancellationToken;
        await using var db = new FatewakeDbContext(new DbContextOptionsBuilder<FatewakeDbContext>().UseNpgsql(cs).Options);
        await db.Database.EnsureDeletedAsync(ct);
        using var mailbox = new TestEmailMailbox();
        using var rsa = RSA.Create(2048);
        var key = new RsaSecurityKey(rsa) { KeyId = "test-provider" };
        const string issuer = "https://accounts.google.com";
        const string audience = "test-client-id";
        await using var factory = new WebApplicationFactory<LocalLoginRequest>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development").UseSetting("ConnectionStrings:fatewake", cs)
                .UseSetting("Email:Directory", mailbox.DirectoryPath)
                .UseSetting("Authentication:Google:Enabled", "true").UseSetting("Authentication:Google:ClientId", audience)
                .UseSetting("Authentication:Microsoft:Enabled", "true").UseSetting("Authentication:Microsoft:ClientId", audience);
            builder.ConfigureServices(services =>
            {
                foreach (var provider in new[] { "Google", "Microsoft" })
                    services.PostConfigure<JwtBearerOptions>(provider, options =>
                    {
                        var metadata = new OpenIdConnectConfiguration { Issuer = issuer };
                        metadata.SigningKeys.Add(key);
                        options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(metadata);
                    });
            });
        });
        using var client = factory.CreateClient();
        string IdToken(string tokenIssuer = issuer, string tokenAudience = audience, bool expired = false)
        {
            var now = DateTime.UtcNow;
            return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(tokenIssuer, tokenAudience,
                [new Claim("sub", "provider-player"), new Claim("email", "provider@example.com"),
                 new Claim("email_verified", "true"), new Claim("name", "Provider Player")],
                now.AddMinutes(-10), expired ? now.AddMinutes(-5) : now.AddMinutes(5),
                new SigningCredentials(key, SecurityAlgorithms.RsaSha256)));
        }
        foreach (var invalid in new[] { IdToken(tokenIssuer: "https://evil.example.com"), IdToken(tokenAudience: "other-client"), IdToken(expired: true), "invalid" })
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", invalid);
            using var rejected = await client.PostAsync("/api/auth/external/google", null, ct);
            Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
        }
        var valid = IdToken();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", valid);
        using var exchanged = await client.PostAsync("/api/auth/external/google", null, ct);
        Assert.Equal(HttpStatusCode.OK, exchanged.StatusCode);
        var apiToken = (await exchanged.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("accessToken").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);
        var info = await client.GetFromJsonAsync<JsonElement>("/api/auth/me", ct);
        Assert.False(info.GetProperty("hasPassword").GetBoolean());
        Assert.True(info.GetProperty("emailVerified").GetBoolean());
        var id = info.GetProperty("accountId").GetGuid();
        using var weakPassword = await client.PostAsJsonAsync("/api/auth/set-password", new PasswordRequest("short"), ct);
        Assert.Equal(HttpStatusCode.BadRequest, weakPassword.StatusCode);
        const string password = "my external account backup password";
        using var configured = await client.PostAsJsonAsync("/api/auth/set-password", new PasswordRequest(password), ct);
        Assert.Equal(HttpStatusCode.OK, configured.StatusCode);
        using var overwrite = await client.PostAsJsonAsync("/api/auth/set-password", new PasswordRequest("overwrite attempt password"), ct);
        Assert.Equal(HttpStatusCode.BadRequest, overwrite.StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        using var localLogin = await client.PostAsJsonAsync("/api/auth/login", new LocalLoginRequest("PROVIDER@example.com", password), ct);
        Assert.Equal(HttpStatusCode.OK, localLogin.StatusCode);
        var localToken = (await localLogin.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("accessToken").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", localToken);
        Assert.Equal(id, (await client.GetFromJsonAsync<JsonElement>("/api/auth/me", ct)).GetProperty("accountId").GetGuid());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", valid);
        using var microsoft = await client.PostAsync("/api/auth/external/microsoft", null, ct);
        Assert.Equal(HttpStatusCode.Accepted, microsoft.StatusCode);
        using var proof = await client.PostAsJsonAsync("/api/auth/verify-email", new VerificationRequest(mailbox.Token("provider@example.com")), ct);
        Assert.Equal(HttpStatusCode.OK, proof.StatusCode);
        var provedToken = (await proof.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("accessToken").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", provedToken);
        Assert.Equal(id, (await client.GetFromJsonAsync<JsonElement>("/api/auth/me", ct)).GetProperty("accountId").GetGuid());
        Assert.Single(await db.Accounts.ToListAsync(ct));
        Assert.Equal(2, await db.ExternalIdentities.CountAsync(ct));
    }

    /// <summary>Verified provider identities link canonical local accounts, while unverified addresses require single-use mailbox proof.</summary>
    [Fact]
    public async Task External_accounts_link_only_after_email_proof_and_can_set_local_password()
    {
        var cs = Environment.GetEnvironmentVariable("FATEWAKE_TEST_CONNECTION");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(cs), "Requires an isolated FATEWAKE_TEST_CONNECTION database.");
        var ct = TestContext.Current.CancellationToken;
        var options = new DbContextOptionsBuilder<FatewakeDbContext>().UseNpgsql(cs).Options;
        await using var db = new FatewakeDbContext(options);
        await db.Database.EnsureDeletedAsync(ct);
        await DatabaseInitializer.InitializeAsync(db, ct);
        using var mailbox = new TestEmailMailbox();
        var hasher = new PasswordHasher<LocalCredentialRecord>();
        var accounts = mailbox.Accounts(db, hasher);
        var verification = mailbox.Verification(db);
        const string password = "a sufficiently long password";
        var pending = await accounts.RegisterAsync("local@example.com", password, ct);
        Assert.True(pending.VerificationRequired);
        var localToken = mailbox.Token("local@example.com");
        Assert.Null((await verification.VerifyAsync(localToken, ct)).AccountId);
        var local = await verification.VerifyAsync(localToken, ct, password);
        Assert.NotNull(local.AccountId);
        Assert.Null((await verification.VerifyAsync(localToken, ct)).AccountId);
        var google = new ExternalLogin("Google", "google-local", "LOCAL@EXAMPLE.COM", true, "Local", null);
        Assert.Equal(local.AccountId, (await verification.ExternalAsync(google, ct)).AccountId);
        Assert.Single(await db.Accounts.ToListAsync(ct));
        Assert.Equal(local.AccountId, (await accounts.LoginAsync("local@example.com", password, ct)).AccountId);
        Assert.Null((await accounts.SetPasswordAsync(local.AccountId!.Value, "do not overwrite existing password", ct)).AccountId);
        var microsoft = new ExternalLogin("Microsoft", "microsoft-local", "local@example.com", true, "Local", null);
        var requiresProof = await verification.ExternalAsync(microsoft, ct);
        Assert.True(requiresProof.VerificationRequired);
        Assert.False(await db.ExternalIdentities.AnyAsync(x => x.Provider == "Microsoft", ct));
        var microsoftProof = mailbox.Token("local@example.com");
        Assert.Equal(local.AccountId, (await verification.VerifyAsync(microsoftProof, ct)).AccountId);
        Assert.Equal(local.AccountId, (await verification.ExternalAsync(microsoft with { Email = "changed@example.com" }, ct)).AccountId);
        Assert.Equal(2, await db.ExternalIdentities.CountAsync(ct));
        var external = await verification.ExternalAsync(new("Google", "google-new", "external@example.com", true, "External", null), ct);
        Assert.NotNull(external.AccountId);
        Assert.False(await db.LocalCredentials.AnyAsync(x => x.AccountId == external.AccountId, ct));
        Assert.Equal(external.AccountId, (await accounts.SetPasswordAsync(external.AccountId!.Value, password, ct)).AccountId);
        Assert.Equal(external.AccountId, (await accounts.LoginAsync("EXTERNAL@example.com", password, ct)).AccountId);
        // An attacker cannot pre-register a password and retain it when the mailbox owner signs in externally.
        await accounts.RegisterAsync("owner@example.com", "attacker chosen password", ct);
        var attackerToken = mailbox.Token("owner@example.com");
        var owner = await verification.ExternalAsync(new("Google", "real-owner", "owner@example.com", true, null, null), ct);
        Assert.NotNull(owner.AccountId);
        Assert.Null((await verification.VerifyAsync(attackerToken, ct)).AccountId);
        Assert.Null((await accounts.LoginAsync("owner@example.com", "attacker chosen password", ct)).AccountId);
        Assert.False(await db.LocalCredentials.AnyAsync(x => x.AccountId == owner.AccountId, ct));
        var unverifiedGoogle = await verification.ExternalAsync(new("Google", "unverified-google", "unverified@example.com", false, null, null), ct);
        Assert.True(unverifiedGoogle.VerificationRequired);
        var expiringToken = mailbox.Token("unverified@example.com");
        var proof = await db.EmailVerifications.SingleAsync(x => x.Email == "unverified@example.com", ct);
        proof.ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1);
        proof.CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-2);
        await db.SaveChangesAsync(ct);
        Assert.Null((await verification.VerifyAsync(expiringToken, ct)).AccountId);
        await verification.ResendAsync("unverified@example.com", ct);
        var resent = mailbox.Token("unverified@example.com");
        Assert.NotEqual(expiringToken, resent);
        var proved = await verification.VerifyAsync(resent, ct);
        Assert.NotNull(proved.AccountId);
        Assert.Null((await verification.VerifyAsync(resent, ct)).AccountId);
        var totalEmails = Directory.GetFiles(mailbox.DirectoryPath, "*.txt").Length;
        await verification.ResendAsync("missing@example.com", ct);
        Assert.Equal(totalEmails, Directory.GetFiles(mailbox.DirectoryPath, "*.txt").Length);
        Assert.All(await db.EmailVerifications.ToListAsync(ct), x => Assert.DoesNotContain(x.TokenHash, new[] { localToken, microsoftProof, attackerToken, resent }));
        Assert.Equal(4, await db.Accounts.CountAsync(ct));
    }

    /// <summary>HTTP login issues usable protected tokens and gameplay rejects foreign accounts, anonymous access to owned survivors, and invalid tokens.</summary>
    [Fact]
    public async Task Local_login_routes_enforce_account_ownership()
    {
        var cs = Environment.GetEnvironmentVariable("FATEWAKE_TEST_CONNECTION");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(cs), "Requires an isolated FATEWAKE_TEST_CONNECTION database.");
        var ct = TestContext.Current.CancellationToken;
        await using var db = new FatewakeDbContext(new DbContextOptionsBuilder<FatewakeDbContext>().UseNpgsql(cs).Options);
        await db.Database.EnsureDeletedAsync(ct);
        using var mailbox = new TestEmailMailbox();
        await using var factory = new WebApplicationFactory<LocalLoginRequest>().WithWebHostBuilder(builder =>
            builder.UseEnvironment("Development").UseSetting("ConnectionStrings:fatewake", cs)
                .UseSetting("Email:Directory", mailbox.DirectoryPath));
        using var client = factory.CreateClient();
        using var anonymousIdentity = await client.GetAsync("/api/auth/me", ct);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousIdentity.StatusCode);
        const string password = "a sufficiently long password";
        using var registration = await client.PostAsJsonAsync("/api/auth/register", new LocalLoginRequest("first@example.com", password), ct);
        Assert.Equal(HttpStatusCode.Accepted, registration.StatusCode);
        using var verifiedRegistration = await client.PostAsJsonAsync("/api/auth/verify-email", new VerificationRequest(mailbox.Token("first@example.com"), password), ct);
        Assert.Equal(HttpStatusCode.OK, verifiedRegistration.StatusCode);
        var registrationToken = await verifiedRegistration.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal(28800, registrationToken.GetProperty("expiresIn").GetInt32());
        using var duplicate = await client.PostAsJsonAsync("/api/auth/register", new LocalLoginRequest("FIRST@EXAMPLE.COM", password), ct);
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        using var wrongPassword = await client.PostAsJsonAsync("/api/auth/login", new LocalLoginRequest("first@example.com", "wrong"), ct);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        using var login = await client.PostAsJsonAsync("/api/auth/login", new LocalLoginRequest("FIRST@example.com", password), ct);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("accessToken").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var identity = await client.GetFromJsonAsync<JsonElement>("/api/auth/me", ct);
        Assert.Equal("first@example.com", identity.GetProperty("email").GetString());
        using var started = await client.PostAsJsonAsync("/api/session/start", new { SurvivorId = (Guid?)null }, ct);
        Assert.Equal(HttpStatusCode.OK, started.StatusCode);
        var session = await started.Content.ReadFromJsonAsync<SessionState>(ct);
        Assert.NotNull(session);
        using var resumed = await client.PostAsJsonAsync("/api/session/start", new { SurvivorId = (Guid?)null }, ct);
        Assert.Equal(session.SurvivorId, (await resumed.Content.ReadFromJsonAsync<SessionState>(ct))!.SurvivorId);
        client.DefaultRequestHeaders.Authorization = null;
        using var forbiddenGuest = await client.PostAsJsonAsync("/api/session/start", new { session.SurvivorId }, ct);
        Assert.Equal(HttpStatusCode.NotFound, forbiddenGuest.StatusCode);
        using var secondRegistration = await client.PostAsJsonAsync("/api/auth/register", new LocalLoginRequest("second@example.com", password), ct);
        Assert.Equal(HttpStatusCode.Accepted, secondRegistration.StatusCode);
        using var secondVerified = await client.PostAsJsonAsync("/api/auth/verify-email", new VerificationRequest(mailbox.Token("second@example.com"), password), ct);
        var secondToken = (await secondVerified.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("accessToken").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secondToken);
        using var forbiddenStart = await client.PostAsJsonAsync("/api/session/start", new { session.SurvivorId }, ct);
        Assert.Equal(HttpStatusCode.NotFound, forbiddenStart.StatusCode);
        var progress = new { session.SurvivorId, session.EventInstanceId, SceneKey = "day1-0617-0643", BeatKey = "choice-wake" };
        using var forbiddenProgress = await client.PostAsJsonAsync("/api/session/progress", progress, ct);
        Assert.Equal(HttpStatusCode.NotFound, forbiddenProgress.StatusCode);
        var action = new
        {
            session.SurvivorId, session.TimelineId, session.EventInstanceId, IdempotencyKey = Guid.NewGuid(),
            Action = new CandidateAction("help_injured_stranger", new Dictionary<string, string>())
        };
        using var forbiddenAction = await client.PostAsJsonAsync("/api/day1/resolve", action, ct);
        Assert.Equal(HttpStatusCode.NotFound, forbiddenAction.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid");
        using var invalidBearer = await client.PostAsJsonAsync("/api/session/start", new { SurvivorId = (Guid?)null }, ct);
        Assert.Equal(HttpStatusCode.Unauthorized, invalidBearer.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var progressSaved = await client.PostAsJsonAsync("/api/session/progress", progress, ct);
        Assert.Equal(HttpStatusCode.OK, progressSaved.StatusCode);
        using var actionSaved = await client.PostAsJsonAsync("/api/day1/resolve", action, ct);
        Assert.Equal(HttpStatusCode.OK, actionSaved.StatusCode);
        Assert.True((await actionSaved.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("accepted").GetBoolean());
    }

    /// <summary>Local credentials enforce uniqueness, durable lockout, password hashing, and account-owned session resumption.</summary>
    [Fact]
    public async Task Local_accounts_support_registration_lockout_and_owned_sessions()
    {
        var cs = Environment.GetEnvironmentVariable("FATEWAKE_TEST_CONNECTION");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(cs), "Requires an isolated FATEWAKE_TEST_CONNECTION database.");
        var ct = TestContext.Current.CancellationToken;
        var options = new DbContextOptionsBuilder<FatewakeDbContext>().UseNpgsql(cs).Options;
        await using var db = new FatewakeDbContext(options);
        await db.Database.EnsureDeletedAsync(ct);
        await DatabaseInitializer.InitializeAsync(db, ct);
        var hasher = new PasswordHasher<LocalCredentialRecord>(Options.Create(new PasswordHasherOptions { IterationCount = 210_000 }));
        using var mailbox = new TestEmailMailbox();
        var accounts = mailbox.Accounts(db, hasher);
        const string password = "a sufficiently long password";
        Assert.Null((await accounts.RegisterAsync("not-an-email", password, ct)).AccountId);
        Assert.Null((await accounts.RegisterAsync("player@example.com", "short", ct)).AccountId);
        Assert.Null((await accounts.RegisterAsync("player@example.com", new string('a', 129), ct)).AccountId);
        var pending = await accounts.RegisterAsync(" Player@Example.com ", password, ct);
        Assert.True(pending.VerificationRequired);
        Assert.Empty(await db.Accounts.ToListAsync(ct));
        Assert.Null((await accounts.LoginAsync("player@example.com", password, ct)).AccountId);
        var registered = await mailbox.Verification(db).VerifyAsync(mailbox.Token("Player@Example.com"), ct, password);
        Assert.NotNull(registered.AccountId);
        Assert.Equal("Player@Example.com", registered.Email);
        Assert.Null((await accounts.RegisterAsync("PLAYER@example.COM", password, ct)).AccountId);
        Assert.Single(await db.Accounts.ToListAsync(ct));
        var credential = await db.LocalCredentials.SingleAsync(ct);
        Assert.NotEqual(password, credential.PasswordHash);
        Assert.Equal(PasswordVerificationResult.Success, hasher.VerifyHashedPassword(credential, credential.PasswordHash, password));
        var unknown = await accounts.LoginAsync("missing@example.com", password, ct);
        var concurrentFailures = await Task.WhenAll(Enumerable.Range(0, 5).Select(async _ =>
        {
            await using var isolated = new FatewakeDbContext(options);
            return await mailbox.Accounts(isolated, hasher)
                .LoginAsync("player@example.com", "incorrect", ct);
        }));
        Assert.All(concurrentFailures, failure => Assert.Equal(unknown.Error, failure.Error));
        db.ChangeTracker.Clear();
        credential = await db.LocalCredentials.SingleAsync(ct);
        Assert.Equal(5, credential.FailedAttempts);
        Assert.True(credential.LockoutEnd > DateTimeOffset.UtcNow);
        Assert.Null((await accounts.LoginAsync("player@example.com", password, ct)).AccountId);
        credential.LockoutEnd = DateTimeOffset.UtcNow.AddSeconds(-1);
        await db.SaveChangesAsync(ct);
        Assert.Equal(registered.AccountId, (await accounts.LoginAsync("PLAYER@example.com", password, ct)).AccountId);
        Assert.Equal(0, credential.FailedAttempts);
        Assert.Null(credential.LockoutEnd);
        var oldHasher = new PasswordHasher<LocalCredentialRecord>(Options.Create(new PasswordHasherOptions { IterationCount = 10_000 }));
        credential.PasswordHash = oldHasher.HashPassword(credential, password);
        await db.SaveChangesAsync(ct);
        Assert.Equal(registered.AccountId, (await accounts.LoginAsync("player@example.com", password, ct)).AccountId);
        Assert.Equal(PasswordVerificationResult.Success, hasher.VerifyHashedPassword(credential, credential.PasswordHash, password));
        var sessions = new SessionStore(db);
        var guest = await sessions.StartOrResumeAsync(null, "test", ct);
        var concurrentSessions = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
        {
            await using var isolated = new FatewakeDbContext(options);
            return await new SessionStore(isolated).StartOrResumeAsync(null, "test", ct, registered.AccountId);
        }));
        var owned = concurrentSessions[0];
        Assert.All(concurrentSessions, session => Assert.Equal(owned.SurvivorId, session.SurvivorId));
        Assert.NotEqual(guest.SurvivorId, owned.SurvivorId);
        Assert.Equal(registered.AccountId, (await db.Survivors.SingleAsync(x => x.Id == owned.SurvivorId, ct)).AccountId);
        Assert.Null((await db.Survivors.SingleAsync(x => x.Id == guest.SurvivorId, ct)).AccountId);
        Assert.Equal(owned.SurvivorId, (await sessions.StartOrResumeAsync(null, "test", ct, registered.AccountId)).SurvivorId);
        await using var restarted = new FatewakeDbContext(options);
        var restartedAccounts = mailbox.Accounts(restarted, hasher);
        Assert.Equal(registered.AccountId, (await restartedAccounts.LoginAsync("player@example.com", password, ct)).AccountId);
        Assert.Equal(owned.SurvivorId, (await new SessionStore(restarted).StartOrResumeAsync(null, "test", ct, registered.AccountId)).SurvivorId);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261006000300_DurableWorkAndArtwork", ct);
        Assert.Equal(2, await db.Survivors.CountAsync(ct));
        Assert.Single(await db.Accounts.ToListAsync(ct));
        await migrator.MigrateAsync(cancellationToken: ct);
        db.ChangeTracker.Clear();
        Assert.Empty(await db.LocalCredentials.ToListAsync(ct));
    }

    /// <summary>The work/artwork migration supports real queue claims and can be rolled back without deleting survivors.</summary>
    [Fact]
    public async Task Durable_work_schema_supports_claims_artifacts_and_rollback()
    {
        var cs = Environment.GetEnvironmentVariable("FATEWAKE_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(cs)) return;
        var ct = TestContext.Current.CancellationToken;
        var options = new DbContextOptionsBuilder<FatewakeDbContext>().UseNpgsql(cs).Options;
        await using var db = new FatewakeDbContext(options);
        await db.Database.EnsureDeletedAsync(ct);
        await DatabaseInitializer.InitializeAsync(db, ct);
        var session = await new SessionStore(db).StartOrResumeAsync(null, "test", ct);
        var signals = new NullWorkSignalBus();
        var builder = new WorkJobBuilder(db, signals, NullLogger<WorkJobBuilder>.Instance);
        var job = await builder.CreateAsync(new WorkJobDefinition("test", Guid.NewGuid().ToString("N"), "{}",
            [new("finalize", "test.finalize", ArtWorkQueues.Finalize, "{}")]), ct);
        var store = new WorkStore(db, signals, NullLogger<WorkStore>.Instance);
        var lease = await store.ClaimAsync(ArtWorkQueues.Finalize, "test-worker", TimeSpan.FromMinutes(1), ct);
        Assert.NotNull(lease);
        Assert.Equal(job.JobId, lease.JobId);
        await new WorkArtifactStore(db).PutAsync(job.JobId, "test", "{\"ready\":true}", ct);
        Assert.True(await store.CompleteAsync(lease.StepId, lease.LeaseToken, "{}", ct));
        Assert.Equal(WorkJobStatus.Completed, (await db.WorkJobs.SingleAsync(ct)).Status);
        Assert.Empty(await db.ArtAssets.ToListAsync(ct));
        Assert.Empty(await db.ArtAssetDerivatives.ToListAsync(ct));
        Assert.Empty(await db.ArtGenerations.ToListAsync(ct));
        Assert.Empty(await db.SurvivorVisualIdentities.ToListAsync(ct));
        Assert.True(new WorkExecutionOptions().QueueConcurrency[ArtWorkQueues.Finalize] > 0);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261006000200_StatusEnums", ct);
        Assert.Equal(session.SurvivorId, (await db.Survivors.SingleAsync(ct)).Id);
        await migrator.MigrateAsync(cancellationToken: ct);
        Assert.Empty(await db.WorkJobs.AsNoTracking().ToListAsync(ct));
    }

    /// <summary>An accepted action persists its resolution, event, and Wake records.</summary>
    [Fact]
    public async Task Accepted_action_persists_resolution_event_and_wake_atomically()
    {
        // Uses PostgreSQL via FATEWAKE_TEST_CONNECTION; deliberately not EF InMemory,
        // because JSONB, transactions and row locking are part of the behavior under test.
        var cs = Environment.GetEnvironmentVariable("FATEWAKE_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(cs)) return;
        var ct = TestContext.Current.CancellationToken;
        var options = new DbContextOptionsBuilder<FatewakeDbContext>().UseNpgsql(cs).Options;
        await using var db = new FatewakeDbContext(options);
        await db.Database.EnsureDeletedAsync(ct);
        await DatabaseInitializer.InitializeAsync(db,ct);
        var realm = await db.Realms.SingleAsync(ct);
        var timelineId=Guid.NewGuid(); var survivorId=Guid.NewGuid(); var eventId=Guid.NewGuid(); var now=DateTimeOffset.UtcNow;
        db.Timelines.Add(new TimelineRecord{Id=timelineId,RealmId=realm.Id,ProgressionState="personal",ConvergenceState="isolated",WorldClockPolicy="activity",CreatedAt=now});
        db.Survivors.Add(new SurvivorRecord{Id=survivorId,TimelineId=timelineId,DisplayName="Test Survivor",IdentityMode="fictional",BroadRegion="test",Status=SurvivorStatus.Active,CreatedAt=now,UpdatedAt=now});
        db.EventInstances.Add(new EventInstanceRecord{Id=eventId,TimelineId=timelineId,SurvivorId=survivorId,EventKey="day-001-injured-stranger",Status=EventInstanceStatus.Active,SurvivorDay=1,StartedAt=now});
        await db.SaveChangesAsync(ct);
        var action=new CandidateAction("help_injured_stranger",new Dictionary<string,string>());
        var result=new DayOneGameEngine().Resolve(new GameSnapshot(survivorId,timelineId,1,"day-001-injured-stranger",new Dictionary<string,string>()),action);
        await new ResolutionStore(db).PersistAsync(eventId,survivorId,timelineId,Guid.NewGuid(),action,result,ct);
        Assert.Single(await db.ActionResolutions.ToListAsync(ct)); Assert.Single(await db.GameEvents.ToListAsync(ct)); Assert.Single(await db.Wakes.ToListAsync(ct));
    }

    /// <summary>The status migration preserves existing rows and supports rollback.</summary>
    [Fact]
    public async Task Status_migration_preserves_existing_data_and_can_roll_back()
    {
        var cs = Environment.GetEnvironmentVariable("FATEWAKE_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(cs)) return;
        var ct = TestContext.Current.CancellationToken;
        var options = new DbContextOptionsBuilder<FatewakeDbContext>().UseNpgsql(cs).Options;
        await using var db = new FatewakeDbContext(options);
        await db.Database.EnsureDeletedAsync(ct);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261006000100_Initial",ct);

        var accountId = Guid.NewGuid();
        var realmId = Guid.NewGuid();
        var timelineId = Guid.NewGuid();
        var survivorId = Guid.NewGuid();
        var episodeId = Guid.NewGuid();
        var gameEventId = Guid.NewGuid();
        var wakeId = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($$"""
INSERT INTO account ("Id", "Status", "CreatedAt", "UpdatedAt")
VALUES ({{accountId}}, 'active', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP);
INSERT INTO realm ("Id", "Key", "Name")
VALUES ({{realmId}}, 'the-silence', 'The Silence');
INSERT INTO timeline ("Id", "RealmId", "ProgressionState", "ConvergenceState", "WorldClockPolicy", "CreatedAt")
VALUES ({{timelineId}}, {{realmId}}, 'personal', 'isolated', 'activity', CURRENT_TIMESTAMP);
INSERT INTO survivor ("Id", "TimelineId", "DisplayName", "IdentityMode", "BroadRegion", "Status", "CreatedAt", "UpdatedAt")
VALUES ({{survivorId}}, {{timelineId}}, 'Test Survivor', 'fictional', 'test', 'active', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP);
INSERT INTO event_instance ("Id", "TimelineId", "SurvivorId", "EventKey", "Status", "SurvivorDay", "StartedAt")
VALUES ({{episodeId}}, {{timelineId}}, {{survivorId}}, 'day-001-injured-stranger', 'active', 1, CURRENT_TIMESTAMP);
INSERT INTO game_event ("Id", "TimelineId", "EventType", "Sequence", "SurvivorDay", "CorrelationId", "Payload", "OccurredAt")
VALUES ({{gameEventId}}, {{timelineId}}, 'action_resolved', 1, 1, {{gameEventId}}, '{}', CURRENT_TIMESTAMP);
INSERT INTO wake ("Id", "TimelineId", "OriginEventId", "WakeType", "Scope", "Severity", "State", "Properties", "CreatedDay", "CreatedAt")
VALUES ({{wakeId}}, {{timelineId}}, {{gameEventId}}, 'test', 'personal', 1, 'active', '{}', 1, CURRENT_TIMESTAMP);
""",ct);

        await migrator.MigrateAsync(cancellationToken:ct);
        Assert.Equal(AccountStatus.Active,(await db.Accounts.SingleAsync(ct)).Status);
        Assert.Equal(SurvivorStatus.Active,(await db.Survivors.SingleAsync(ct)).Status);
        Assert.Equal(EventInstanceStatus.Active,(await db.EventInstances.SingleAsync(ct)).Status);
        Assert.Equal(WakeState.Active,(await db.Wakes.SingleAsync(ct)).State);
        var columnTypes = await db.Database.SqlQueryRaw<string>("""
SELECT data_type AS "Value" FROM information_schema.columns
WHERE table_schema = 'public' AND
((table_name IN ('account', 'survivor', 'event_instance') AND column_name = 'Status')
 OR (table_name = 'wake' AND column_name = 'State'))
""").ToListAsync(ct);
        Assert.Equal(4,columnTypes.Count);
        Assert.All(columnTypes,type=>Assert.Equal("integer",type));
        Assert.Equal(1,await db.Database.SqlQueryRaw<int>("""
SELECT count(*)::integer AS "Value" FROM pg_indexes
WHERE schemaname = 'public' AND indexname = 'IX_event_instance_SurvivorId_Status_SurvivorDay'
""").SingleAsync(ct));

        var session = await new SessionStore(db).StartOrResumeAsync(survivorId,"test",ct);
        Assert.Equal(episodeId,session.EventInstanceId);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(session,new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.Equal("active",json.RootElement.GetProperty("status").GetString());

        await migrator.MigrateAsync("20261006000100_Initial",ct);
        var legacyStatuses = await db.Database.SqlQueryRaw<string>("""
SELECT "Status" AS "Value" FROM account
UNION ALL SELECT "Status" FROM survivor
UNION ALL SELECT "Status" FROM event_instance
UNION ALL SELECT "State" FROM wake
""").ToListAsync(ct);
        Assert.Equal(4,legacyStatuses.Count);
        Assert.All(legacyStatuses,status=>Assert.Equal("active",status));
        await migrator.MigrateAsync(cancellationToken:ct);
    }

    /// <summary>The status migration rejects unsupported values without discarding stored data.</summary>
    [Fact]
    public async Task Status_migration_rejects_unsupported_values_without_losing_data()
    {
        var cs = Environment.GetEnvironmentVariable("FATEWAKE_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(cs)) return;
        var ct = TestContext.Current.CancellationToken;
        var options = new DbContextOptionsBuilder<FatewakeDbContext>().UseNpgsql(cs).Options;
        await using var db = new FatewakeDbContext(options);
        await db.Database.EnsureDeletedAsync(ct);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261006000100_Initial",ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
INSERT INTO account ("Id", "Status", "CreatedAt", "UpdatedAt")
VALUES ({Guid.NewGuid()}, 'unsupported', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
""",ct);

        var error = await Assert.ThrowsAsync<PostgresException>(()=>migrator.MigrateAsync(cancellationToken:ct));
        Assert.Equal(PostgresErrorCodes.NotNullViolation,error.SqlState);
        Assert.Equal("unsupported",await db.Database.SqlQueryRaw<string>("""SELECT "Status" AS "Value" FROM account""").SingleAsync(ct));
        Assert.Single(await db.Database.GetAppliedMigrationsAsync(ct));
    }
}
