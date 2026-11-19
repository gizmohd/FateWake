using System.Globalization;
using System.Text.Json;
using Fatewake.Infrastructure.Authentication;
using Fatewake.Infrastructure.Story;
using Fatewake.Story;
using Microsoft.EntityFrameworkCore;

namespace Fatewake.Infrastructure.Persistence;

/// <summary>Enforces player consent and story location rules for private conversations.</summary>
public sealed class PlayerChatService(FatewakeDbContext db)
{
    private const string Nearby = "Nearby";
    private const string Remote = "Remote";
    /// <summary>Maximum length of one player message.</summary>
    public const int MessageLimit = 400;

    /// <summary>Updates the account's participation in player chat.</summary>
    public Task SetEnabledAsync(Guid player, bool enabled, CancellationToken ct) => db.Accounts
        .Where(account => account.Id == player && account.Status == AccountStatus.Active)
        .ExecuteUpdateAsync(update => update.SetProperty(account => account.PlayerChatEnabled, enabled), ct);

    /// <summary>Lists nearby opt-in players and existing friend relationships.</summary>
    public async Task<ChatLobby> ReadLobbyAsync(Guid player, Guid survivor, CancellationToken ct)
    {
        if (!await db.Accounts.AnyAsync(account => account.Id == player && account.Status == AccountStatus.Active &&
                account.PlayerChatEnabled, ct))
            return new(false, []);

        var current = await CurrentAsync(player, survivor, ct);
        var friends = await db.Set<PlayerFriendRecord>().AsNoTracking()
            .Where(row => row.FirstPlayer == player || row.SecondPlayer == player).ToArrayAsync(ct);
        var friendsByPlayer = friends.ToDictionary(row => row.FirstPlayer == player ? row.SecondPlayer : row.FirstPlayer);
        var friendIds = friendsByPlayer.Keys.ToArray();
        var positions = Positions().Where(peer => peer.Player != player);
        if (current is not null)
        {
            positions = positions.Where(peer => friendIds.Contains(peer.Player) ||
                (peer.Realm == current.Realm && peer.Clock == current.Clock && peer.Tick == current.Tick &&
                    (peer.Timeline == current.Timeline || peer.IsOriginal && current.IsOriginal) &&
                    (peer.Cell == current.Cell || peer.Room != null && peer.Room == current.Room)));
        }
        else
        {
            positions = positions.Where(peer => friendIds.Contains(peer.Player));
        }
        var visible = await positions.OrderBy(peer => peer.Player).ThenBy(peer => peer.Survivor).ToArrayAsync(ct);
        var contacts = visible.GroupBy(peer => peer.Player).ToDictionary(group => group.Key, group =>
        {
            var peer = group.FirstOrDefault(peer => LocalScope(current, peer) is not null) ?? group.First();
            friendsByPlayer.TryGetValue(peer.Player, out var friend);
            return new ChatContact(peer.Player, Label(peer.Name, peer.Player), LocalScope(current, peer) is not null,
                RemoteScope(current, peer, friend) is not null, Friendship(friend, player));
        });
        foreach (var (id, friend) in friendsByPlayer)
            contacts.TryAdd(id, new(id, Label("Survivor", id), false, false, Friendship(friend, player)));
        return new(true, contacts.Values.OrderByDescending(contact => contact.Nearby).ThenBy(contact => contact.Name).ToArray());
    }

    /// <summary>Returns the visible transcript and rechecks communication availability on every read.</summary>
    public async Task<ChatConversation> ReadAsync(Guid player, Guid survivor, Guid peer, CancellationToken ct)
    {
        if (player == peer)
            return new(null, []);
        var (first, second) = Pair(player, peer);
        var current = await CurrentAsync(player, survivor, ct);
        var other = await PeerAsync(peer, current, ct);
        var friend = await FindFriend(first, second, ct);
        var local = LocalScope(current, other);
        var remote = RemoteScope(current, other, friend);
        if (local is null && remote is null)
            return new(null, []);

        var messages = await db.Set<PlayerMessageRecord>().AsNoTracking().Where(message =>
                message.FirstPlayer == first && message.SecondPlayer == second &&
                (message.Scope == local || message.Scope == remote) && message.StoryTick <= current!.Tick)
            .OrderByDescending(message => message.Id).Take(50).ToArrayAsync(ct);
        var level = (await AccountProfileService.GetContentAsync(db, player, ct)).Level;
        return new(local is null ? Remote : Nearby, messages.Reverse()
            .Where(message => StoryContentPolicy.Allows(level, message.Text))
            .Select(message => new ChatLine(message.Id, message.Sender == player, message.Text)).ToArray());
    }

    /// <summary>Writes a message only while both players can use the same means of communication.</summary>
    public async Task<string?> SendAsync(Guid player, Guid survivor, Guid peer, string? text, CancellationToken ct)
    {
        var message = text?.Trim();
        if (message is null || message.Length is < 1 or > MessageLimit || message.Contains('\0'))
            return "Enter a message of 1–400 characters.";
        if (player == peer)
            return "Choose another player.";

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockPlayersAsync(player, peer, ct);
        var (first, second) = Pair(player, peer);
        var current = await CurrentAsync(player, survivor, ct);
        var other = await PeerAsync(peer, current, ct);
        var scope = LocalScope(current, other) ?? RemoteScope(current, other, await FindFriend(first, second, ct));
        if (scope is null)
            return "You can no longer reach this player.";
        var senderLevel = (await AccountProfileService.GetContentAsync(db, player, ct)).Level;
        var recipientLevel = (await AccountProfileService.GetContentAsync(db, peer, ct)).Level;
        var level = senderLevel < recipientLevel ? senderLevel : recipientLevel;
        if (!StoryContentPolicy.Allows(level, message))
            return "That message exceeds this conversation’s content settings.";

        db.Add(new PlayerMessageRecord { FirstPlayer = first, SecondPlayer = second, Sender = player,
            Scope = scope, StoryTick = current!.Tick, SentAt = DateTimeOffset.UtcNow, Text = message });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return null;
    }

    /// <summary>Requests a friendship while nearby, accepts an incoming request, or removes the relationship.</summary>
    public async Task<string?> ChangeFriendAsync(Guid player, Guid survivor, Guid peer, string action, CancellationToken ct)
    {
        if (player == peer)
            return "Choose another player.";
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockPlayersAsync(player, peer, ct);
        var (first, second) = Pair(player, peer);
        var existing = await FindFriend(first, second, ct);
        switch (action)
        {
            case "request" when existing is null:
                var current = await CurrentAsync(player, survivor, ct);
                if (LocalScope(current, await PeerAsync(peer, current, ct)) is null)
                    return "Friend requests can be sent to nearby players who have enabled chat.";
                db.Add(new PlayerFriendRecord { FirstPlayer = first, SecondPlayer = second, RequestedBy = player });
                break;
            case "accept" when existing is { Accepted: false } && existing.RequestedBy == peer:
                var enabled = await db.Accounts.CountAsync(account => (account.Id == player || account.Id == peer) &&
                    account.Status == AccountStatus.Active && account.PlayerChatEnabled, ct);
                if (enabled != 2)
                    return "Both players must enable chat before accepting.";
                existing.Accepted = true;
                break;
            case "remove" when existing is not null:
                db.Remove(existing);
                break;
            default:
                return "That friend request has changed. Refresh and try again.";
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return null;
    }

    private Task<PlayerFriendRecord?> FindFriend(Guid first, Guid second, CancellationToken ct) =>
        db.Set<PlayerFriendRecord>().SingleOrDefaultAsync(row => row.FirstPlayer == first && row.SecondPlayer == second, ct);

    private async Task LockPlayersAsync(Guid player, Guid peer, CancellationToken ct)
    {
        // Match the session lock order and serialize acceptance, sends and movements across API instances.
        await db.Accounts.FromSqlInterpolated($"""
            SELECT * FROM account WHERE "Id" IN ({player}, {peer}) ORDER BY "Id" FOR UPDATE
            """).AsNoTracking().ToArrayAsync(ct);
        await db.Survivors.FromSqlInterpolated($"""
            SELECT * FROM survivor WHERE "AccountId" IN ({player}, {peer}) ORDER BY "Id" FOR UPDATE
            """).AsNoTracking().ToArrayAsync(ct);
    }

    private Task<ChatPosition?> CurrentAsync(Guid player, Guid survivor, CancellationToken ct) =>
        Positions().SingleOrDefaultAsync(row => row.Player == player && row.Survivor == survivor, ct);

    private async Task<ChatPosition?> PeerAsync(Guid peer, ChatPosition? current, CancellationToken ct)
    {
        if (current is null)
            return null;
        var positions = await Positions().Where(row => row.Player == peer).OrderBy(row => row.Survivor).ToArrayAsync(ct);
        return positions.FirstOrDefault(position => LocalScope(current, position) is not null) ?? positions.FirstOrDefault();
    }

    private IQueryable<ChatPosition> Positions()
    {
        var visits = db.Set<WorldVisitRecord>().AsNoTracking();
        return from who in db.Survivors.AsNoTracking()
               join account in db.Accounts.AsNoTracking() on who.AccountId equals (Guid?)account.Id
               join visit in visits on new { who.TimelineId, SurvivorId = who.Id } equals new { visit.TimelineId, visit.SurvivorId }
               join cell in db.Set<WorldCellRecord>().AsNoTracking() on visit.CellId equals cell.Id
               where account.PlayerChatEnabled && account.Status == AccountStatus.Active && who.Status == SurvivorStatus.Active &&
                   !visits.Any(later => later.SurvivorId == who.Id && later.TimelineId == who.TimelineId && later.Sequence > visit.Sequence)
               select new ChatPosition
               {
                   Player = account.Id, Survivor = who.Id, Name = who.DisplayName, Timeline = visit.TimelineId,
                   Realm = cell.RealmId, Cell = cell.Id, Room = cell.RoomKey, Clock = visit.ClockKey, Tick = visit.Tick,
                   Network = visit.CommunicationNetwork,
                   IsOriginal = !db.Set<TimelineBranchRecord>()
                       .Any(branch => branch.TimelineId == who.TimelineId && branch.ParentTimelineId != null)
               };
    }

    private static bool SameWorld(ChatPosition first, ChatPosition second) =>
        first.Realm == second.Realm && first.Clock == second.Clock &&
        (first.Timeline == second.Timeline || first.IsOriginal && second.IsOriginal);

    private static string? LocalScope(ChatPosition? first, ChatPosition? second)
    {
        if (first is null || second is null || !SameWorld(first, second) || first.Tick != second.Tick ||
            (first.Cell != second.Cell && (first.Room is null || first.Room != second.Room)))
            return null;
        return Scope(first, Nearby, first.Room is null ? $"cell:{first.Cell:N}" : $"room:{first.Room}",
            first.Tick.ToString(CultureInfo.InvariantCulture));
    }

    private static string? RemoteScope(ChatPosition? first, ChatPosition? second, PlayerFriendRecord? friend)
    {
        if (friend?.Accepted != true || first is null || second is null || !SameWorld(first, second) ||
            first.Network is null || first.Network != second.Network)
            return null;
        return Scope(first, Remote, first.Network, null);
    }

    private static string Scope(ChatPosition at, string channel, string place, string? tick) => JsonSerializer.Serialize(
        new[] { channel, at.Realm.ToString("N"), at.IsOriginal ? "original" : at.Timeline.ToString("N"), at.Clock, place, tick });

    private static (Guid First, Guid Second) Pair(Guid a, Guid b) => a.CompareTo(b) < 0 ? (a, b) : (b, a);
    private static string Label(string name, Guid player) => $"{name} · {player.ToString("N")[..6]}";
    private static string? Friendship(PlayerFriendRecord? friend, Guid player) =>
        friend is null ? null : friend.Accepted ? "friend" : friend.RequestedBy == player ? "sent" : "received";

    private sealed class ChatPosition
    {
        public Guid Player { get; init; }
        public Guid Survivor { get; init; }
        public required string Name { get; init; }
        public Guid Timeline { get; init; }
        public Guid Realm { get; init; }
        public Guid Cell { get; init; }
        public string? Room { get; init; }
        public required string Clock { get; init; }
        public long Tick { get; init; }
        public string? Network { get; init; }
        public bool IsOriginal { get; init; }
    }
}
