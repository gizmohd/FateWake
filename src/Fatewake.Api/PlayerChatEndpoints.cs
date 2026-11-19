using System.Security.Claims;
using Fatewake.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fatewake.Api;

/// <summary>Exposes authenticated, survivor-owned player chat and friend actions.</summary>
public static class PlayerChatEndpoints
{
    /// <summary>Maps player chat routes.</summary>
    public static void MapPlayerChat(this WebApplication app)
    {
        var group = app.MapGroup("/api/survivors/{survivor:guid}/chat").RequireAuthorization();
        group.MapGet("", (Guid survivor, ClaimsPrincipal user, FatewakeDbContext db, PlayerChatService chat, CancellationToken ct) =>
            Query(survivor, user, db, async player => Results.Ok(await chat.ReadLobbyAsync(player, survivor, ct)), ct));
        group.MapPut("/preferences", (Guid survivor, ChatPreferences request, ClaimsPrincipal user, FatewakeDbContext db,
            PlayerChatService chat, CancellationToken ct) => Query(survivor, user, db, async player =>
        {
            await chat.SetEnabledAsync(player, request.Enabled, ct);
            return Results.NoContent();
        }, ct)).RequireRateLimiting("player-chat-write");
        group.MapGet("/{peer:guid}", (Guid survivor, Guid peer, ClaimsPrincipal user, FatewakeDbContext db,
            PlayerChatService chat, CancellationToken ct) => Query(survivor, user, db,
            async player => Results.Ok(await chat.ReadAsync(player, survivor, peer, ct)), ct));
        group.MapPost("/{peer:guid}/messages", (Guid survivor, Guid peer, MessageRequest request, ClaimsPrincipal user,
            FatewakeDbContext db, PlayerChatService chat, CancellationToken ct) => Query(survivor, user, db,
            async player => Changed(await chat.SendAsync(player, survivor, peer, request.Text, ct)), ct))
            .RequireRateLimiting("player-chat-write");
        group.MapPost("/{peer:guid}/friendship", (Guid survivor, Guid peer, FriendRequest request, ClaimsPrincipal user,
            FatewakeDbContext db, PlayerChatService chat, CancellationToken ct) => Query(survivor, user, db,
            async player => Changed(await chat.ChangeFriendAsync(player, survivor, peer, request.Action, ct)), ct))
            .RequireRateLimiting("player-chat-write");
    }

    private static async Task<IResult> Query(Guid survivor, ClaimsPrincipal user, FatewakeDbContext db,
        Func<Guid, Task<IResult>> run, CancellationToken ct)
    {
        if (!Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var account))
            return Results.Unauthorized();
        var owns = await db.Survivors.AnyAsync(row => row.Id == survivor && row.AccountId == account, ct);
        return owns ? await run(account) : Results.NotFound();
    }

    private static IResult Changed(string? error) => error is null
        ? Results.NoContent()
        : Results.BadRequest(new { error });

    private sealed record ChatPreferences(bool Enabled);
    private sealed record MessageRequest(string? Text);
    private sealed record FriendRequest(string Action);
}
