using Fatewake.GameEngine;
using Fatewake.GameEngine.DayOne;
using Fatewake.Infrastructure.Persistence;\nusing Fatewake.AI.Intent;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddDbContext<FatewakeDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("fatewake")));
builder.Services.AddSingleton<IGameEngine, DayOneGameEngine>();\nbuilder.Services.AddScoped<IResolutionStore, ResolutionStore>();\nbuilder.Services.AddScoped<ISessionStore, SessionStore>();\nbuilder.Services.AddSingleton<IIntentInterpreter, BaselineIntentInterpreter>();
var app = builder.Build();\nawait using (var scope = app.Services.CreateAsyncScope())\n{\n    var db = scope.ServiceProvider.GetRequiredService<FatewakeDbContext>();\n    await DatabaseInitializer.InitializeAsync(db);\n}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapPost("/api/day1/resolve", (CandidateAction action, IGameEngine engine) =>
{
    var state = new GameSnapshot(Guid.Empty, Guid.Empty, 1, "day-001-injured-stranger", new Dictionary<string,string>());
    return Results.Ok(engine.Resolve(state, action));
});
app.Run();\n\npublic sealed record StartSessionRequest(Guid? SurvivorId, string? BroadRegion);\npublic sealed record DayOneResolveRequest(Guid EventInstanceId, Guid SurvivorId, Guid TimelineId, CandidateAction Action);
