using Fatewake.AI.Intent;
using Fatewake.Api.Authentication;
using Fatewake.GameEngine;
using Fatewake.GameEngine.DayOne;
using Fatewake.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

builder.Services.AddDbContext<FatewakeDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("fatewake")));
builder.Services.AddSingleton<IGameEngine, DayOneGameEngine>();
builder.Services.AddScoped<IResolutionStore, ResolutionStore>();
builder.Services.AddScoped<ISessionStore, SessionStore>();
builder.Services.AddScoped<IAccountStore, AccountStore>();
builder.Services.AddSingleton<IIntentInterpreter, BaselineIntentInterpreter>();
builder.Services.Configure<ExternalIdentityOptions>(
    builder.Configuration.GetSection("Authentication"));

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FatewakeDbContext>();
    await DatabaseInitializer.InitializeAsync(db);
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/api/session/start", async (
    StartSessionRequest request,
    ISessionStore sessions,
    CancellationToken ct) =>
    Results.Ok(await sessions.StartOrResumeAsync(
        request.SurvivorId, request.BroadRegion ?? "unknown", ct)));

app.MapPost("/api/session/progress", async (
    PresentationProgressRequest request,
    ISessionStore sessions,
    CancellationToken ct) =>
{
    var state = await sessions.SavePresentationProgressAsync(request.SurvivorId, request.EventInstanceId, request.SceneKey, request.BeatKey, ct);
    return state is null ? Results.NotFound() : Results.Ok(state);
});

app.MapPost("/api/day1/resolve", async (
    DayOneResolveRequest request,
    IGameEngine engine,
    IResolutionStore store,
    IIntentInterpreter intents,
    CancellationToken ct) =>
{
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

    var resolution = engine.Resolve(state, action);
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

public sealed record StartSessionRequest(Guid? SurvivorId, string? BroadRegion);
public sealed record PresentationProgressRequest(Guid SurvivorId, Guid EventInstanceId, string SceneKey, string BeatKey);
public sealed record DayOneResolveRequest(
    Guid EventInstanceId,
    Guid SurvivorId,
    Guid TimelineId,
    Guid IdempotencyKey,
    CandidateAction Action);
