using Fatewake.GameEngine;
using Fatewake.GameEngine.DayOne;
using Fatewake.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddDbContext<FatewakeDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("fatewake")));
builder.Services.AddSingleton<IGameEngine, DayOneGameEngine>();\nbuilder.Services.AddScoped<IResolutionStore, ResolutionStore>();
var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapPost("/api/day1/resolve", (CandidateAction action, IGameEngine engine) =>
{
    var state = new GameSnapshot(Guid.Empty, Guid.Empty, 1, "day-001-injured-stranger", new Dictionary<string,string>());
    return Results.Ok(engine.Resolve(state, action));
});
app.Run();\n\npublic sealed record DayOneResolveRequest(Guid EventInstanceId, Guid SurvivorId, Guid TimelineId, CandidateAction Action);
