using Fatewake.Infrastructure.Persistence;
using Fatewake.Story;
using Microsoft.EntityFrameworkCore;

namespace Fatewake.IntegrationTests;

/// <summary>Verifies the coordinate grid and authoritative visit history.</summary>
public sealed class WorldMapTests
{
    /// <summary>Co-presence requires the same Realm, cell, Thread, clock and tick.</summary>
    [Fact]
    public void Separates_space_from_world_time()
    {
        var cell = new RealmCell(Guid.NewGuid(), 0, 0);
        var point = new WorldPosition(Guid.NewGuid(), cell, "clock", 377);
        Assert.True(cell.IsNeighbor(cell with { Q = -1, R = 1 }));
        Assert.False(cell.IsNeighbor(cell with { Q = 1, R = 1 }));
        Assert.True(point.IsCoPresent(point with { }));
        Assert.False(point.IsCoPresent(point with { Tick = 378 }));
        Assert.False(point.IsCoPresent(point with { ClockKey = "other-clock" }));
        Assert.False(point.IsCoPresent(point with { TimelineId = Guid.NewGuid() }));
        Assert.False(point.IsCoPresent(point with { Cell = cell with { RealmId = Guid.NewGuid() } }));
    }

    /// <summary>Story milestones update the map, and an authorized hop changes the Realm and clock without changing the Thread.</summary>
    [Fact]
    public async Task Records_story_time_and_cross_realm_hops()
    {
        var connection = Environment.GetEnvironmentVariable("FATEWAKE_REGISTRATION_TEST_CONNECTION");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(connection), "An isolated PostgreSQL database is required.");
        var ct = TestContext.Current.CancellationToken;
        await using var db = new FatewakeDbContext(new DbContextOptionsBuilder<FatewakeDbContext>().UseNpgsql(connection).Options);
        await DatabaseInitializer.InitializeAsync(db, ct);
        var sessionStore = new SessionStore(db);
        var session = await sessionStore.StartOrResumeAsync(null, "unknown", ct);
        var maps = new WorldMapStore(db);

        var opening = await maps.ReadAsync(session.SurvivorId, ct);
        Assert.Equal(377, opening.Current!.Position.Tick);
        Assert.Equal("Home", opening.Current.Place);
        await sessionStore.SavePresentationProgressAsync(session.SurvivorId, session.EventInstanceId, session.SceneKey, "house-flicker", ct);
        await sessionStore.SavePresentationProgressAsync(session.SurvivorId, session.EventInstanceId, session.SceneKey, "street-reveal", ct);
        var doorway = (await maps.ReadAsync(session.SurvivorId, ct)).Current!;
        Assert.Equal("Doorway", doorway.Place);
        Assert.Equal(379, doorway.Position.Tick);
        Assert.True(opening.Current.Position.Cell.IsNeighbor(doorway.Position.Cell));

        var realm = Guid.NewGuid();
        db.Realms.Add(new() { Id = realm, Key = realm.ToString("N"), Name = "Test Realm" });
        db.Add(new WorldCellRecord { Id = Guid.NewGuid(), RealmId = realm, Q = 1, R = 0, Name = "Arrival" });
        await db.SaveChangesAsync(ct);
        var destination = new WorldPosition(session.TimelineId, new(realm, 1, 0), "arrival-clock", 12);
        await maps.RecordHopAsync(session.SurvivorId, destination, doorway.Sequence, ct);
        await Assert.ThrowsAsync<TimelineConflictException>(() =>
            maps.RecordHopAsync(session.SurvivorId, destination, doorway.Sequence, ct));
        await sessionStore.StartOrResumeAsync(session.SurvivorId, "unknown", ct);
        await sessionStore.SavePresentationProgressAsync(session.SurvivorId, session.EventInstanceId, session.SceneKey, "first-choice", ct);

        var after = await maps.ReadAsync(session.SurvivorId, ct);
        Assert.True(after.Current!.IsHop);
        Assert.Equal(destination, after.Current.Position);
        Assert.Equal("Test Realm", after.Current.Realm);
        Assert.Equal(4, after.Visits.Count);

        var peer = await sessionStore.StartOrResumeAsync(null, "unknown", ct);
        var peerRow = await db.Survivors.SingleAsync(row => row.Id == peer.SurvivorId, ct);
        var destinationCell = await db.Set<WorldCellRecord>().SingleAsync(cell => cell.RealmId == realm, ct);
        db.Add(new WorldVisitRecord { SurvivorId = peer.SurvivorId, TimelineId = peer.TimelineId, Sequence = 2,
            CellId = destinationCell.Id, ClockKey = destination.ClockKey, Tick = 12 });
        await db.SaveChangesAsync(ct);
        Assert.Empty((await maps.ReadAsync(session.SurvivorId, ct)).CoPresent);

        peerRow.TimelineId = session.TimelineId;
        db.Add(new WorldVisitRecord { SurvivorId = peer.SurvivorId, TimelineId = session.TimelineId, Sequence = 1,
            CellId = destinationCell.Id, ClockKey = destination.ClockKey, Tick = 13 });
        await db.SaveChangesAsync(ct);
        Assert.Empty((await maps.ReadAsync(session.SurvivorId, ct)).CoPresent);
        await maps.RecordHopAsync(peer.SurvivorId, destination, 1, ct);
        Assert.Equal(peerRow.DisplayName, Assert.Single((await maps.ReadAsync(session.SurvivorId, ct)).CoPresent));
    }
}
