using System.Security.Claims;
using Fatewake.Infrastructure.Persistence;
using Fatewake.Infrastructure.Story;
using Microsoft.EntityFrameworkCore;
namespace Fatewake.Api;
/// <summary>Exposes audience-owned story history without private causes or generation side effects.</summary>
public static class StoryEndpoints
{
    /// <summary>Maps read-only novel history and first-display artwork pinning.</summary>
    public static void MapStory(this WebApplication app)
    {
        app.MapGet("/api/story/{survivorId:guid}/notices", async (Guid survivorId, HttpContext context, FatewakeDbContext db, StoryJournalService story, CancellationToken ct) =>
        {
            if (InvalidBearer(context))
                return Results.Unauthorized();
            if (!await OwnsAsync(db, survivorId, context.User, ct))
                return Results.NotFound();
            return Results.Ok(await story.NoticesAsync(survivorId, ct));
        });
        app.MapPost("/api/story/{survivorId:guid}/notices/{panelId:guid}/{revision:int}/ack", async (Guid survivorId, Guid panelId, int revision, HttpContext context, FatewakeDbContext db, StoryJournalService story, CancellationToken ct) =>
        {
            if (InvalidBearer(context))
                return Results.Unauthorized();
            if (!await OwnsAsync(db, survivorId, context.User, ct))
                return Results.NotFound();
            return await story.AcknowledgeAsync(survivorId, panelId, revision, ct) ? Results.Ok() : Results.BadRequest();
        });
        app.MapGet("/api/story/{survivorId:guid}", async (Guid survivorId, HttpContext context, FatewakeDbContext db, StoryJournalService story, CancellationToken ct) =>
        {
            if (InvalidBearer(context))
                return Results.Unauthorized();
            if (!await OwnsAsync(db, survivorId, context.User, ct))
                return Results.NotFound();
            return Results.Ok(await story.ReadAsync(survivorId, ct));
        });
        app.MapGet("/api/story/{survivorId:guid}/scene", async (Guid survivorId, bool? retry, HttpContext context, FatewakeDbContext db, IStoryPlayService play, CancellationToken ct) =>
        {
            if (InvalidBearer(context))
                return Results.Unauthorized();
            if (!await OwnsAsync(db, survivorId, context.User, ct))
                return Results.NotFound();
            return Results.Ok(await play.GetCurrentAsync(survivorId, retry == true, ct));
        });
        app.MapPost("/api/story/{survivorId:guid}/scene/{sequence:int}/advance", async (Guid survivorId, int sequence, HttpContext context, FatewakeDbContext db, IStoryPlayService play, CancellationToken ct) =>
        {
            if (InvalidBearer(context))
                return Results.Unauthorized();
            if (!await OwnsAsync(db, survivorId, context.User, ct))
                return Results.NotFound();
            try
            {
                return Results.Ok(await play.AdvanceAsync(survivorId, sequence, ct));
            }
            catch (InvalidOperationException)
            {
                return Results.Conflict();
            }
        });
        app.MapPost("/api/story/{survivorId:guid}/scene/{sequence:int}/choose", async (Guid survivorId, int sequence, StoryChoiceRequest request, HttpContext context, FatewakeDbContext db, IStoryPlayService play, CancellationToken ct) =>
        {
            if (InvalidBearer(context))
                return Results.Unauthorized();
            if (!await OwnsAsync(db, survivorId, context.User, ct))
                return Results.NotFound();
            try
            {
                return await play.ChooseAsync(survivorId, sequence, request.ActionType, request.RawInput, ct) is { } view
                    ? Results.Ok(view) : Results.BadRequest();
            }
            catch (InvalidOperationException)
            {
                return Results.Conflict();
            }
        });
        app.MapPost("/api/story/{survivorId:guid}/artwork", async (Guid survivorId, StoryArtworkRequest request, HttpContext context, FatewakeDbContext db, StoryJournalService story, CancellationToken ct) =>
        {
            if (InvalidBearer(context))
                return Results.Unauthorized();
            if (!await OwnsAsync(db, survivorId, context.User, ct))
                return Results.NotFound();
            if (request.Versions is null || request.Versions.Count > 24 || request.Versions.Values.Any(x => x is <= 0))
                return Results.BadRequest();
            return await story.PinArtworkAsync(survivorId, request.EventInstanceId, request.BeatKey, request.Versions, ct) ? Results.Ok() : Results.BadRequest();
        });
    }
    private static bool InvalidBearer(HttpContext context) => context.Request.Headers.ContainsKey("Authorization") && context.User.Identity?.IsAuthenticated != true;
    private static Task<bool> OwnsAsync(FatewakeDbContext db, Guid survivorId, ClaimsPrincipal user, CancellationToken ct)
    {
        Guid? account = Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
        return db.Survivors.AnyAsync(x => x.Id == survivorId && x.AccountId == account, ct);
    }
}
