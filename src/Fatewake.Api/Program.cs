using Fatewake.AI.Intent;
using Fatewake.Api;
using Fatewake.Api.Authentication;
using Fatewake.GameEngine;
using Fatewake.GameEngine.DayOne;
using Fatewake.Infrastructure.Art;
using Fatewake.Infrastructure.Authentication;
using Fatewake.Infrastructure.Persistence;
using Fatewake.Infrastructure.Rag;
using Fatewake.Infrastructure.Story;
using Fatewake.Infrastructure.Work;
using Fatewake.Observability;
using Fatewake.Story;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddSqlDataProtection(builder.Configuration, "Fatewake.Api");
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = RagIngestionLimits.MaxRequestBytes);
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = RagIngestionLimits.MaxDocumentBytes;
    options.MultipartHeadersLengthLimit = 64 * 1024;
});
builder.AddRedisDistributedCache("redis");
builder.Services.AddAuthentication(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)
    .AddExternalTokenValidation(builder.Configuration);
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("IdentityAdministrator", policy =>
    policy.RequireAuthenticatedUser().RequireRole("Admin")
        .RequireAssertion(context => Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out _) &&
            context.User.FindFirstValue(OpenIddictEndpoints.ClientClaim) == (builder.Configuration["OpenIddictWeb:ClientId"] ?? "fatewake-web")));
builder.Services.AddScoped<AdminApplicationEndpoints>();
builder.Services.AddScoped<AdminUserEndpoints>();
builder.Services.AddOpenIddict()
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore()
            .UseDbContext<FatewakeDbContext>();
    })
    .AddServer(options =>
    {
        options.SetIssuer(new Uri(builder.Configuration["OpenIddict:Issuer"] ?? "https://localhost:63281"));
        options.SetAuthorizationEndpointUris("/connect/authorize");
        options.SetTokenEndpointUris("/connect/token");
        options.SetEndSessionEndpointUris("/connect/logout");
        options.AllowAuthorizationCodeFlow()
            .AllowRefreshTokenFlow();
        options.RegisterScopes(OpenIddictConstants.Scopes.OpenId,
            OpenIddictConstants.Scopes.Profile,
            OpenIddictConstants.Scopes.Email,
            OpenIddictConstants.Scopes.OfflineAccess,
            "fatewake-api");
        options.SetAccessTokenLifetime(TimeSpan.FromMinutes(30));
        options.SetRefreshTokenLifetime(TimeSpan.FromDays(30));
        if (builder.Environment.IsDevelopment())
        {
            options.AddDevelopmentEncryptionCertificate()
                .AddDevelopmentSigningCertificate();
        }
        else
        {
            options.AddSigningKey(ReadOpenIddictKey(builder.Configuration, "OpenIddict:SigningKey"));
            options.AddEncryptionKey(ReadOpenIddictKey(builder.Configuration, "OpenIddict:EncryptionKey"));
        }
        options.UseAspNetCore()
            .EnableAuthorizationEndpointPassthrough()
            .EnableTokenEndpointPassthrough()
            .EnableEndSessionEndpointPassthrough();
    })
    .AddValidation(options =>
    {
        options.SetIssuer(builder.Configuration["OpenIddict:Issuer"] ?? "https://localhost:63281");
        options.AddAudiences(builder.Configuration["OpenIddict:ApiAudience"] ?? "fatewake-api");
        options.UseLocalServer();
        options.EnableTokenEntryValidation();
        options.UseAspNetCore();
    });
builder.Services.AddArtReviewAuthorization(builder.Configuration);
builder.Services.Configure<PasswordHasherOptions>(options => options.IterationCount = 210_000);
builder.Services.AddScoped<IPasswordHasher<LocalCredentialRecord>, PasswordHasher<LocalCredentialRecord>>();
builder.Services.AddScoped<LocalAccountService>();
builder.Services.AddScoped<AccountProfileService>();
builder.Services.AddScoped<VerifiedAccountService>();
builder.Services.AddSingleton<VerificationEmailSender>();
builder.Services.AddOptions<EmailDeliveryOptions>().Bind(builder.Configuration.GetSection("Email"))
    .Validate(options => Uri.TryCreate(options.PublicWebUrl, UriKind.Absolute, out var url) &&
        (url.Scheme == "https" || builder.Environment.IsDevelopment() && url.Scheme == "http"), "Email:PublicWebUrl must be an HTTPS URL.")
    .Validate(options => !options.SmtpEnabled || !string.IsNullOrWhiteSpace(options.Host), "Enabled SMTP requires Email:Host.")
    .Validate(options => options.Port is > 0 and <= 65535, "Invalid SMTP port.")
    .ValidateOnStart();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("timeline-mutation", context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 6, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("player-chat-write", context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("local-auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

builder.Services.AddDbContext<FatewakeDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("fatewake")));
builder.Services.AddSingleton<IGameEngine, DayOneGameEngine>();
builder.Services.AddScoped<IResolutionStore, ResolutionStore>();
builder.Services.AddScoped<Fatewake.Observability.IAiUsageRecorder, Fatewake.Infrastructure.Usage.AiUsageRecorder>();
builder.Services.AddScoped<Fatewake.Infrastructure.Usage.AdminDashboardService>();
builder.Services.AddScoped<ISessionStore, SessionStore>();
builder.Services.AddScoped<TimelineStateStore>();
builder.Services.AddScoped<TimelineBranchService>();
builder.Services.AddScoped<PlayerChatService>();
builder.Services.AddScoped<StoryJournalService>();
builder.Services.AddScoped<IRagIngestionService, RagIngestionService>();
builder.Services.AddScoped<RagClaimService>();
builder.Services.AddScoped<IRagStagingStore, RagStagingStore>();
builder.Services.AddScoped<IAccountStore, AccountStore>();
builder.Services.Configure<ArtStorageOptions>(builder.Configuration.GetSection(ArtStorageOptions.SectionName));
var artStorage = builder.Configuration.GetSection(ArtStorageOptions.SectionName).Get<ArtStorageOptions>() ?? new ArtStorageOptions();
switch (artStorage.Provider.ToLowerInvariant())
{
    case "postgres":
        builder.Services.AddScoped<IArtBinaryStorage, PostgresArtBinaryStorage>();
        builder.Services.AddScoped<ArtBinaryBackfillService>();
        break;
    case "filesystem":
        var artStorageRoot = artStorage.RootPath ?? builder.Configuration["Art:StorageRoot"];
        if (string.IsNullOrWhiteSpace(artStorageRoot))
            artStorageRoot = Path.Combine(builder.Environment.ContentRootPath, "data", "art");
        builder.Services.AddScoped<IArtBinaryStorage>(_ => new FileSystemArtBinaryStorage(Path.GetFullPath(artStorageRoot)));
        break;
    default:
        throw new InvalidOperationException($"Unsupported ArtStorage provider '{artStorage.Provider}'.");
}
builder.Services.AddScoped<IArtAssetStore, ArtAssetStore>();
builder.Services.AddScoped<IArtAssetIngestionService, ArtAssetIngestionService>();
builder.Services.AddScoped<IArtResolutionService, ArtResolutionService>();
builder.Services.Configure<RabbitMqWorkOptions>(builder.Configuration.GetSection("RabbitMq"));
builder.Services.AddSingleton<IWorkSignalBus, RabbitMqWorkSignalBus>();
builder.Services.AddScoped<IWorkJobBuilder, WorkJobBuilder>();
builder.Services.AddScoped<IArtWorkJobFactory, ArtWorkJobFactory>();
builder.Services.AddSingleton<SceneArtCatalog>();
builder.Services.AddScoped<ArtPromptJobFactory>();
builder.Services.AddScoped<ISceneScheduler, SceneScheduler>();
builder.Services.AddScoped<IStoryPlayService, StoryPlayService>();
builder.Services.AddScoped<SceneArtPromptService>();
builder.Services.AddScoped<ArtPromptSeedImporter>();
builder.Services.AddKeyedSingleton<IArtImageProcessor, SkiaSharpArtImageProcessor>("skia");
builder.Services.AddSingleton<IArtImageProcessor, SkiaSharpArtImageProcessor>();
builder.Services.AddSingleton<IIntentInterpreter, BaselineIntentInterpreter>();
builder.Services.Configure<ExternalIdentityOptions>(
    builder.Configuration.GetSection("Authentication"));

var app = builder.Build();
app.Use(async (context, next) =>
{
    using var operation = OperationTelemetry.Start("api.request");
    try
    {
        await next(context);
    }
    catch (Exception ex) when ((context.Request.Path.StartsWithSegments("/api/auth") || context.Request.Path == "/connect/authorize") &&
        (ex is System.Net.Mail.SmtpException or IOException))
    {
        operation.Fail(ex);
        app.Logger.LogError(ex, "Email delivery or storage operation failed");
        if (context.Response.HasStarted)
            throw;
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        await context.Response.WriteAsJsonAsync(new
        {
            error = "Email delivery is unavailable. Please retry or request another verification email."
        });
    }
    catch (TimelineConflictException ex)
    {
        app.Logger.LogWarning("Timeline operation rejected because its state or prerequisites changed");
        context.Response.StatusCode = StatusCodes.Status409Conflict;
        await context.Response.WriteAsJsonAsync(new
        {
            error = ex.Message
        });
    }
    catch (OperationCanceledException ex) when (context.RequestAborted.IsCancellationRequested)
    {
        operation.Fail(ex);
        if (!context.Response.HasStarted)
            context.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
    }
    catch (Exception ex) { operation.Fail(ex); throw; }
});
app.UseMiddleware<RagRequestDiagnostics>();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapSceneArt();
app.MapArtReviews();
app.MapStory();
app.MapTimelines();
app.MapPlayerChat();
app.MapAdminDashboard();
#if DEBUG
DebugArtworkReset.Map(app);
#endif
app.MapRagAdmin();
app.MapOpenIddict();
AdminApplicationEndpoints.Map(app);
AdminUserEndpoints.Map(app);

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FatewakeDbContext>();
    await DatabaseInitializer.InitializeAsync(db);
    await scope.ServiceProvider.GetRequiredService<ArtPromptSeedImporter>().InitializeAsync();
    await OpenIddictEndpoints.SeedAsync(scope.ServiceProvider, app.Configuration);
    if (string.Equals(artStorage.Provider, "Postgres", StringComparison.OrdinalIgnoreCase))
        await scope.ServiceProvider.GetRequiredService<ArtBinaryBackfillService>().BackfillAsync();
}

app.MapGet("/health", async (FatewakeDbContext db, CancellationToken ct) =>
    await db.Database.CanConnectAsync(ct)
        ? Results.Ok(new
        {
            status = "ok"
        })
        : Results.StatusCode(StatusCodes.Status503ServiceUnavailable));

app.MapPost("/api/auth/register", async (LocalRegistrationRequest request, LocalAccountService accounts, CancellationToken ct) =>
{
    var result = await accounts.RegisterAsync(request.Email, request.Password, ct,
        new(request.FirstName, request.LastName, request.City, request.State, request.BirthDate));
    return result.VerificationRequired ? Results.Accepted(value: new
    {
        verificationRequired = true
    })
        : result.AccountId is not null ? Results.NoContent() : Results.BadRequest(new
        {
            error = result.Error
        });
}).RequireRateLimiting("local-auth");

app.MapPut("/api/auth/profile", async (AccountProfileDetails request, ClaimsPrincipal user, AccountProfileService profiles, CancellationToken ct) =>
{
    if (AccountId(user) is not { } accountId)
        return Results.Unauthorized();
    var error = await profiles.CompleteAsync(accountId, request, ct);
    return error is null ? Results.NoContent() : Results.BadRequest(new
    {
        error
    });
}).RequireAuthorization().RequireRateLimiting("local-auth");

app.MapGet("/api/auth/settings", async (ClaimsPrincipal user, FatewakeDbContext db, CancellationToken ct) =>
    AccountId(user) is { } accountId
        ? Results.Ok(await AccountProfileService.GetContentAsync(db, accountId, ct))
        : Results.Unauthorized()).RequireAuthorization();

app.MapPut("/api/auth/settings", async (ContentPreferenceUpdate request, ClaimsPrincipal user, AccountProfileService profiles, CancellationToken ct) =>
{
    if (AccountId(user) is not { } accountId)
        return Results.Unauthorized();
    var error = await profiles.UpdateContentAsync(accountId, request, ct);
    return error is null ? Results.NoContent() : Results.BadRequest(new { error });
}).RequireAuthorization().RequireRateLimiting("local-auth");

app.MapPost("/api/auth/resend-verification", async (EmailRequest request, VerifiedAccountService verification, CancellationToken ct) =>
{
    await verification.ResendAsync(request.Email, ct);
    return Results.Accepted(value: new
    {
        message = "If verification is needed, an email has been sent."
    });
}).RequireRateLimiting("local-auth");
app.MapPost("/api/auth/set-password", async (PasswordRequest request, ClaimsPrincipal user, LocalAccountService accounts, CancellationToken ct) =>
{
    var result = await accounts.SetPasswordAsync(AccountId(user)!.Value, request.Password, ct);
    return result.AccountId is null ? Results.BadRequest(new
    {
        error = result.Error
    }) : Results.Ok();
}).RequireAuthorization().RequireRateLimiting("local-auth");
app.MapGet("/api/auth/me", async (ClaimsPrincipal user, FatewakeDbContext db, CancellationToken ct) =>
{
    var accountId = AccountId(user);
    var profile = await db.AccountProfiles.AsNoTracking().SingleOrDefaultAsync(row => row.AccountId == accountId, ct);
    return Results.Ok(new
    {
        accountId,
        role = user.FindFirstValue(ClaimTypes.Role),
        email = user.FindFirstValue(ClaimTypes.Email),
        requiresProfile = profile is { IsComplete: false },
        hasPassword = await db.LocalCredentials.AnyAsync(x => x.AccountId == accountId, ct),
        emailVerified = await db.AccountEmails.AnyAsync(x => x.AccountId == accountId && x.Verified, ct)
    });
}).RequireAuthorization();

app.MapPost("/api/session/start", async (
    StartSessionRequest request,
    ISessionStore sessions,
    StoryJournalService story,
    HttpContext context,
    ClaimsPrincipal user,
    FatewakeDbContext db,
    CancellationToken ct) =>
{
    if (InvalidBearer(context))
        return Results.Unauthorized();
    var accountId = AccountId(user);
    if (request.SurvivorId is { } survivorId && !await CanAccess(db, survivorId, accountId, ct))
        return Results.NotFound();
    var state = await sessions.StartOrResumeAsync(request.SurvivorId, request.BroadRegion ?? "unknown", ct, accountId);
    return Results.Ok(state);
});

app.MapGet("/api/survivors/{survivorId:guid}/map", async (Guid survivorId, HttpContext context,
    FatewakeDbContext db, CancellationToken ct) =>
{
    if (InvalidBearer(context))
        return Results.Unauthorized();
    if (!await CanAccess(db, survivorId, AccountId(context.User), ct))
        return Results.NotFound();
    return Results.Ok(await new WorldMapStore(db).ReadAsync(survivorId, ct));
});

app.MapPost("/api/session/progress", async (
    PresentationProgressRequest request,
    ISessionStore sessions,
    StoryJournalService story,
    HttpContext context,
    ClaimsPrincipal user,
    FatewakeDbContext db,
    CancellationToken ct) =>
{
    if (InvalidBearer(context))
        return Results.Unauthorized();
    if (!await CanAccess(db, request.SurvivorId, AccountId(user), ct))
        return Results.NotFound();
    var state = await sessions.SavePresentationProgressAsync(request.SurvivorId, request.EventInstanceId, request.SceneKey, request.BeatKey, ct);
    return state is null ? Results.NotFound() : Results.Ok(state);
});

if (app.Environment.IsDevelopment())
{
    app.MapPost("/api/debug/session/reset-chapter", async (
        ResetChapterRequest request,
        ISessionStore sessions,
        StoryJournalService story,
        HttpContext context,
        ClaimsPrincipal user,
        FatewakeDbContext db,
        CancellationToken ct) =>
    {
        if (InvalidBearer(context))
            return Results.Unauthorized();
        if (!await CanAccess(db, request.SurvivorId, AccountId(user), ct))
            return Results.NotFound();
        var state = await sessions.ResetChapterAsync(request.SurvivorId, request.EventInstanceId, ct);
        return state is null ? Results.NotFound() : Results.Ok(state);
    });
}

app.MapPost("/api/day1/resolve", async (
    DayOneResolveRequest request,
    IGameEngine engine,
    IResolutionStore store,
    StoryJournalService story,
    IIntentInterpreter intents,
    HttpContext context,
    ClaimsPrincipal user,
    FatewakeDbContext db,
    CancellationToken ct) =>
{
    if (InvalidBearer(context))
        return Results.Unauthorized();
    if (!await CanAccess(db, request.SurvivorId, AccountId(user), ct) ||
        !await db.EventInstances.AnyAsync(x => x.Id == request.EventInstanceId && x.SurvivorId == request.SurvivorId &&
            x.TimelineId == request.TimelineId && x.EventKey != DayOneScenes.GeneratedOpeningKey, ct))
        return Results.NotFound();
    var state = await new TimelineStateStore(db).LoadAsync(
        request.SurvivorId, request.TimelineId, request.EventInstanceId, ct);

    var action = request.Action;
    if (action.ActionType == SceneChoiceResolver.Freeform)
    {
        var choice = SceneChoiceResolver.Resolve([], true, action.ActionType, action.RawInput);
        var preferences = await AccountProfileService.GetContentAsync(db, AccountId(user), ct);
        if (choice.FreeformText is not { } text || !StoryContentPolicy.Allows(preferences.Level, text))
            return Results.Ok(new ActionResolution(false, "action_content_rejected", [], [],
                new Dictionary<string, string>(), "day1-v1"));

        action = await intents.InterpretAsync(state, text, ct);
        if (action.ActionType == "unrecognized_intent")
            action = action with { ActionType = "improvise" };
    }
    else if (action.RawInput is not null || action.ActionType == "improvise")
        return Results.BadRequest();

    ActionResolution resolution;
    using (OperationTelemetry.Start("game.resolve"))
        resolution = engine.Resolve(state, action);
    if (resolution.Accepted)
    {
        resolution = await store.PersistAsync(
            request.EventInstanceId,
            request.SurvivorId,
            request.TimelineId,
            request.IdempotencyKey,
            action,
            resolution,
            ct);
    }

    return Results.Ok(resolution);
});

app.Run();

static Guid? AccountId(ClaimsPrincipal user) =>
    Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

static bool InvalidBearer(HttpContext context) =>
    context.Request.Headers.ContainsKey("Authorization") && context.User.Identity?.IsAuthenticated != true;

static Task<bool> CanAccess(FatewakeDbContext db, Guid survivorId, Guid? accountId, CancellationToken ct) =>
    db.Survivors.AnyAsync(x => x.Id == survivorId && x.AccountId == accountId, ct);

static RsaSecurityKey ReadOpenIddictKey(IConfiguration configuration, string name)
{
    var pem = configuration[name];
    if (string.IsNullOrWhiteSpace(pem))
        throw new InvalidOperationException($"{name} requires an RSA private key in PEM format.");
    using var rsa = RSA.Create();
    rsa.ImportFromPem(pem);
    if (rsa.KeySize < 2048)
        throw new InvalidOperationException($"{name} must be at least 2048 bits.");
    return new RsaSecurityKey(rsa.ExportParameters(includePrivateParameters: true));
}

