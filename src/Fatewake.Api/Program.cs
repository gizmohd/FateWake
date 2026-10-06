using Fatewake.AI.Intent;
using Fatewake.Api;
using Fatewake.Api.Authentication;
using Fatewake.GameEngine;
using Fatewake.GameEngine.DayOne;
using Fatewake.Infrastructure.Persistence;
using Fatewake.Infrastructure.Art;
using Fatewake.Infrastructure.Work;
using Microsoft.EntityFrameworkCore;
using Fatewake.Infrastructure.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Fatewake.Observability;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddAuthentication(BearerTokenDefaults.AuthenticationScheme).AddBearerToken(options =>
    options.BearerTokenExpiration = TimeSpan.FromHours(8)).AddExternalTokenValidation(builder.Configuration);
builder.Services.AddAuthorization();
builder.Services.Configure<PasswordHasherOptions>(options => options.IterationCount = 210_000);
builder.Services.AddScoped<IPasswordHasher<LocalCredentialRecord>, PasswordHasher<LocalCredentialRecord>>();
builder.Services.AddScoped<LocalAccountService>();
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
    options.AddPolicy("local-auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

builder.Services.AddDbContext<FatewakeDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("fatewake")));
builder.Services.AddSingleton<IGameEngine, DayOneGameEngine>();
builder.Services.AddScoped<IResolutionStore, ResolutionStore>();
builder.Services.AddScoped<ISessionStore, SessionStore>();
builder.Services.AddScoped<IAccountStore, AccountStore>();
var artStorageRoot=builder.Configuration["ArtStorage:RootPath"] ?? builder.Configuration["Art:StorageRoot"];
if(string.IsNullOrWhiteSpace(artStorageRoot))
    artStorageRoot=Path.Combine(builder.Environment.ContentRootPath,"data","art");
builder.Services.AddSingleton<IArtBinaryStorage>(_=>new FileSystemArtBinaryStorage(artStorageRoot));
builder.Services.AddScoped<IArtAssetStore, ArtAssetStore>();
builder.Services.AddScoped<IArtAssetIngestionService, ArtAssetIngestionService>();
builder.Services.AddScoped<IArtResolutionService, ArtResolutionService>();
builder.Services.Configure<RabbitMqWorkOptions>(builder.Configuration.GetSection("RabbitMq"));
builder.Services.AddSingleton<IWorkSignalBus, RabbitMqWorkSignalBus>();
builder.Services.AddScoped<IWorkJobBuilder, WorkJobBuilder>();
builder.Services.AddScoped<IArtWorkJobFactory, ArtWorkJobFactory>();
builder.Services.AddKeyedSingleton<IArtImageProcessor, SkiaSharpArtImageProcessor>("skia");
builder.Services.AddKeyedSingleton<IArtImageProcessor, ImageSharpArtImageProcessor>("imagesharp");
builder.Services.AddSingleton<IArtImageProcessor, SkiaSharpArtImageProcessor>();
builder.Services.AddSingleton<IIntentInterpreter, BaselineIntentInterpreter>();
builder.Services.Configure<ExternalIdentityOptions>(
    builder.Configuration.GetSection("Authentication"));

var app = builder.Build();
app.Use(async (context, next) =>
{
    using var operation = OperationTelemetry.Start("api.request");
    try { await next(context); }
    catch (Exception ex) when (context.Request.Path.StartsWithSegments("/api/auth") && (ex is System.Net.Mail.SmtpException or IOException))
    {
        operation.Fail(ex);
        app.Logger.LogError(ex, "Email delivery or storage operation failed");
        if (context.Response.HasStarted) throw;
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        await context.Response.WriteAsJsonAsync(new { error = "Email delivery is unavailable. Please retry or request another verification email." });
    }
    catch (Exception ex) { operation.Fail(ex); throw; }
});
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FatewakeDbContext>();
    await DatabaseInitializer.InitializeAsync(db);
}

app.MapGet("/health", async (FatewakeDbContext db, CancellationToken ct) =>
    await db.Database.CanConnectAsync(ct)
        ? Results.Ok(new { status = "ok" })
        : Results.StatusCode(StatusCodes.Status503ServiceUnavailable));

app.MapPost("/api/auth/register", async (LocalLoginRequest request, LocalAccountService accounts, CancellationToken ct) =>
{
    var result = await accounts.RegisterAsync(request.Email, request.Password, ct);
    return result.VerificationRequired ? Results.Accepted(value: new { verificationRequired = true })
        : Results.BadRequest(new { error = result.Error });
}).RequireRateLimiting("local-auth");

app.MapPost("/api/auth/login", async (LocalLoginRequest request, LocalAccountService accounts, CancellationToken ct) =>
{
    var result = await accounts.LoginAsync(request.Email, request.Password, ct);
    if (result.VerificationRequired) return Results.Json(new { verificationRequired = true, error = result.Error }, statusCode: 403);
    return result.AccountId is null ? Results.Json(new { error = result.Error }, statusCode: 401) : SignIn(result);
}).RequireRateLimiting("local-auth");

app.MapExternalAuthentication(SignIn);
app.MapPost("/api/auth/verify-email", async (VerificationRequest request, VerifiedAccountService verification, CancellationToken ct) =>
{
    var result = await verification.VerifyAsync(request.Token, ct, request.Password);
    return result.AccountId is null ? Results.BadRequest(new { error = result.Error }) : SignIn(result);
}).RequireRateLimiting("local-auth");
app.MapPost("/api/auth/resend-verification", async (EmailRequest request, VerifiedAccountService verification, CancellationToken ct) =>
{
    await verification.ResendAsync(request.Email, ct);
    return Results.Accepted(value: new { message = "If verification is needed, an email has been sent." });
}).RequireRateLimiting("local-auth");
app.MapPost("/api/auth/set-password", async (PasswordRequest request, ClaimsPrincipal user, LocalAccountService accounts, CancellationToken ct) =>
{
    var result = await accounts.SetPasswordAsync(AccountId(user)!.Value, request.Password, ct);
    return result.AccountId is null ? Results.BadRequest(new { error = result.Error }) : Results.Ok();
}).RequireAuthorization().RequireRateLimiting("local-auth");
app.MapGet("/api/auth/me", async (ClaimsPrincipal user, FatewakeDbContext db, CancellationToken ct) =>
{
    var accountId = AccountId(user);
    return Results.Ok(new
    {
        accountId,
        email = user.FindFirstValue(ClaimTypes.Email),
        hasPassword = await db.LocalCredentials.AnyAsync(x => x.AccountId == accountId, ct),
        emailVerified = await db.AccountEmails.AnyAsync(x => x.AccountId == accountId && x.Verified, ct)
    });
}).RequireAuthorization();

app.MapPost("/api/session/start", async (
    StartSessionRequest request,
    ISessionStore sessions,
    HttpContext context,
    ClaimsPrincipal user,
    FatewakeDbContext db,
    CancellationToken ct) =>
{
    if (InvalidBearer(context)) return Results.Unauthorized();
    var accountId = AccountId(user);
    if (request.SurvivorId is {} survivorId && !await CanAccess(db, survivorId, accountId, ct))
        return Results.NotFound();
    return Results.Ok(await sessions.StartOrResumeAsync(
        request.SurvivorId, request.BroadRegion ?? "unknown", ct, accountId));
});

app.MapPost("/api/session/progress", async (
    PresentationProgressRequest request,
    ISessionStore sessions,
    HttpContext context,
    ClaimsPrincipal user,
    FatewakeDbContext db,
    CancellationToken ct) =>
{
    if (InvalidBearer(context)) return Results.Unauthorized();
    if (!await CanAccess(db, request.SurvivorId, AccountId(user), ct)) return Results.NotFound();
    var state = await sessions.SavePresentationProgressAsync(request.SurvivorId, request.EventInstanceId, request.SceneKey, request.BeatKey, ct);
    return state is null ? Results.NotFound() : Results.Ok(state);
});

app.MapPost("/api/day1/resolve", async (
    DayOneResolveRequest request,
    IGameEngine engine,
    IResolutionStore store,
    IIntentInterpreter intents,
    HttpContext context,
    ClaimsPrincipal user,
    FatewakeDbContext db,
    CancellationToken ct) =>
{
    if (InvalidBearer(context)) return Results.Unauthorized();
    if (!await CanAccess(db, request.SurvivorId, AccountId(user), ct) ||
        !await db.EventInstances.AnyAsync(x => x.Id == request.EventInstanceId && x.SurvivorId == request.SurvivorId &&
            x.TimelineId == request.TimelineId, ct)) return Results.NotFound();
    var state = new GameSnapshot(
        request.SurvivorId,
        request.TimelineId,
        1,
        "day-001-injured-stranger",
        new Dictionary<string, string>());

    var action = request.Action.ActionType == "freeform" &&
                 !string.IsNullOrWhiteSpace(request.Action.RawInput)
        ? await intents.InterpretAsync(state, request.Action.RawInput, ct)
        : request.Action;

    ActionResolution resolution;
    using (OperationTelemetry.Start("game.resolve"))
        resolution = engine.Resolve(state, action);
    if (resolution.Accepted)
        await store.PersistAsync(
            request.EventInstanceId,
            request.SurvivorId,
            request.TimelineId,
            request.IdempotencyKey,
            action,
            resolution,
            ct);

    return Results.Ok(resolution);
});

app.Run();

static Guid? AccountId(ClaimsPrincipal user) =>
    Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

static bool InvalidBearer(HttpContext context) =>
    context.Request.Headers.ContainsKey("Authorization") && context.User.Identity?.IsAuthenticated != true;

static Task<bool> CanAccess(FatewakeDbContext db, Guid survivorId, Guid? accountId, CancellationToken ct) =>
    db.Survivors.AnyAsync(x => x.Id == survivorId && x.AccountId == accountId, ct);

static IResult SignIn(LocalAccountResult result)
{
    var identity = new ClaimsIdentity([
        new Claim(ClaimTypes.NameIdentifier, result.AccountId!.Value.ToString()),
        new Claim(ClaimTypes.Email, result.Email!),
        new Claim(ClaimTypes.Name, result.Email!)
    ], BearerTokenDefaults.AuthenticationScheme);
    return Results.SignIn(new ClaimsPrincipal(identity), authenticationScheme: BearerTokenDefaults.AuthenticationScheme);
}
