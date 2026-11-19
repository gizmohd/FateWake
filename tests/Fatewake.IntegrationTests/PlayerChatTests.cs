using System.Net;
using System.Net.Http.Json;
using Fatewake.Api;
using Fatewake.Infrastructure.Persistence;
using Fatewake.Story;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Fatewake.IntegrationTests;

/// <summary>Verifies that chat follows consent, physical space, world time and story networks.</summary>
public sealed class PlayerChatTests
{
    /// <summary>Two players can meet across cells in the same room and reach each other remotely only as connected friends.</summary>
    [Fact]
    public async Task Chat_requires_co_presence_or_an_accepted_friend_on_a_working_network()
    {
        var connection = Environment.GetEnvironmentVariable("FATEWAKE_REGISTRATION_TEST_CONNECTION");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(connection), "An isolated PostgreSQL database is required.");
        var ct = TestContext.Current.CancellationToken;
        await using var db = new FatewakeDbContext(new DbContextOptionsBuilder<FatewakeDbContext>().UseNpgsql(connection).Options);
        await DatabaseInitializer.InitializeAsync(db, ct);

        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var stranger = Guid.NewGuid();
        db.Accounts.AddRange(new[] { a, b, stranger }.Select(id => new AccountRecord
        {
            Id = id, Status = AccountStatus.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
        }));
        var realm = await db.Realms.SingleAsync(row => row.Key == "the-silence", ct);
        var room = $"room-{Guid.NewGuid():N}";
        var row = Random.Shared.Next(10000, int.MaxValue);
        db.AddRange(
            new WorldCellRecord { Id = Guid.NewGuid(), RealmId = realm.Id, Q = 0, R = row, Name = "Window", RoomKey = room },
            new WorldCellRecord { Id = Guid.NewGuid(), RealmId = realm.Id, Q = 1, R = row, Name = "Door", RoomKey = room },
            new WorldCellRecord { Id = Guid.NewGuid(), RealmId = realm.Id, Q = 2, R = row, Name = "Outside" });
        await db.SaveChangesAsync(ct);

        var sessions = new SessionStore(db);
        var first = await sessions.StartOrResumeAsync(null, "unknown", ct, a);
        var second = await sessions.StartOrResumeAsync(null, "unknown", ct, b);
        var third = await sessions.StartOrResumeAsync(null, "unknown", ct, stranger);
        Assert.NotEqual(first.TimelineId, second.TimelineId);
        var maps = new WorldMapStore(db);
        var chat = new PlayerChatService(db);
        await Hop(first, 0, 500);
        await Hop(second, 1, 500);
        await Hop(third, 1, 501);

        Assert.False((await chat.ReadLobbyAsync(a, first.SurvivorId, ct)).Enabled);
        await chat.SetEnabledAsync(a, true, ct);
        await chat.SetEnabledAsync(stranger, true, ct);
        Assert.Empty((await chat.ReadLobbyAsync(a, first.SurvivorId, ct)).Contacts);
        await chat.SetEnabledAsync(b, true, ct);
        var nearby = Assert.Single((await chat.ReadLobbyAsync(a, first.SurvivorId, ct)).Contacts);
        Assert.Equal(b, nearby.PlayerId);
        Assert.True(nearby.Nearby);
        Assert.NotNull(await chat.SendAsync(a, first.SurvivorId, b, "Damn.", ct));
        Assert.Null(await chat.SendAsync(a, first.SurvivorId, b, "Can you hear me?", ct));
        Assert.Equal("Can you hear me?", Assert.Single((await chat.ReadAsync(b, second.SurvivorId, a, ct)).Messages).Text);
        Assert.NotNull(await chat.SendAsync(stranger, third.SurvivorId, b, "Hello", ct));

        Assert.Null(await chat.ChangeFriendAsync(a, first.SurvivorId, b, "request", ct));
        Assert.NotNull(await chat.ChangeFriendAsync(a, first.SurvivorId, b, "accept", ct));
        await Hop(first, 0, 501, "radio");
        await Hop(second, 2, 502, "radio");
        Assert.NotNull(await chat.SendAsync(a, first.SurvivorId, b, "Radio check", ct));
        Assert.Null(await chat.ChangeFriendAsync(b, second.SurvivorId, a, "accept", ct));
        Assert.Null(await chat.SendAsync(a, first.SurvivorId, b, "Radio check", ct));
        var remote = await chat.ReadAsync(b, second.SurvivorId, a, ct);
        Assert.Equal("Remote", remote.Channel);
        Assert.Equal("Radio check", Assert.Single(remote.Messages).Text);
        Assert.Null(await chat.SendAsync(b, second.SurvivorId, a, "Copy", ct));
        Assert.Single((await chat.ReadAsync(a, first.SurvivorId, b, ct)).Messages);
        await Hop(first, 0, 502, "radio");
        Assert.Equal(2, (await chat.ReadAsync(a, first.SurvivorId, b, ct)).Messages.Count);

        await Hop(second, 2, 502);
        Assert.Null((await chat.ReadAsync(a, first.SurvivorId, b, ct)).Channel);
        Assert.NotNull(await chat.SendAsync(a, first.SurvivorId, b, "Still there?", ct));
        await chat.SetEnabledAsync(b, false, ct);
        await Hop(second, 1, 502, "radio");
        Assert.Null((await chat.ReadAsync(a, first.SurvivorId, b, ct)).Channel);

        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseTestServer();
        builder.Services.AddDbContext<FatewakeDbContext>(options => options.UseNpgsql(connection));
        builder.Services.AddAuthentication("test-player")
            .AddScheme<AuthenticationSchemeOptions, TimelineTestAuthenticationHandler>("test-player", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddScoped<PlayerChatService>();
        builder.Services.AddRateLimiter(options =>
            options.AddFixedWindowLimiter("player-chat-write", limiter =>
            {
                limiter.PermitLimit = 30;
                limiter.Window = TimeSpan.FromMinutes(1);
            }));
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
        app.MapPlayerChat();
        await app.StartAsync(ct);
        using var client = app.GetTestClient();
        var endpoint = $"/api/survivors/{first.SurvivorId}/chat";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(endpoint, ct)).StatusCode);
        client.DefaultRequestHeaders.Add("X-Test-Account", a.ToString());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/survivors/{second.SurvivorId}/chat", ct)).StatusCode);
        Assert.True((await client.GetFromJsonAsync<ChatLobby>(endpoint, ct))!.Enabled);
        var refused = await client.PostAsJsonAsync($"{endpoint}/{b}/messages", new { text = "Come in" }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);

        async Task Hop(SessionState session, int q, long tick, string? network = null)
        {
            var visit = (await maps.ReadAsync(session.SurvivorId, ct)).Current!;
            await maps.RecordHopAsync(session.SurvivorId, new(session.TimelineId, new RealmCell(realm.Id, q, row),
                "chat-test", tick), visit.Sequence, ct, network);
        }
    }
}
