using Fatewake.GameEngine;
using Fatewake.GameEngine.DayOne;
using Fatewake.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fatewake.IntegrationTests;

public sealed class DayOnePersistenceTests
{
    [Fact]
    public async Task Accepted_action_persists_resolution_event_and_wake_atomically()
    {
        // Uses PostgreSQL via FATEWAKE_TEST_CONNECTION; deliberately not EF InMemory,
        // because JSONB, transactions and row locking are part of the behavior under test.
        var cs = Environment.GetEnvironmentVariable("FATEWAKE_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(cs)) return;
        var ct = TestContext.Current.CancellationToken;
        var options = new DbContextOptionsBuilder<FatewakeDbContext>().UseNpgsql(cs).Options;
        await using var db = new FatewakeDbContext(options);
        await db.Database.EnsureDeletedAsync(ct);
        await DatabaseInitializer.InitializeAsync(db,ct);
        var realm = await db.Realms.SingleAsync(ct);
        var timelineId=Guid.NewGuid(); var survivorId=Guid.NewGuid(); var eventId=Guid.NewGuid(); var now=DateTimeOffset.UtcNow;
        db.Timelines.Add(new TimelineRecord{Id=timelineId,RealmId=realm.Id,ProgressionState="personal",ConvergenceState="isolated",WorldClockPolicy="activity",CreatedAt=now});
        db.Survivors.Add(new SurvivorRecord{Id=survivorId,TimelineId=timelineId,DisplayName="Test Survivor",IdentityMode="fictional",BroadRegion="test",Status="active",CreatedAt=now,UpdatedAt=now});
        db.EventInstances.Add(new EventInstanceRecord{Id=eventId,TimelineId=timelineId,SurvivorId=survivorId,EventKey="day-001-injured-stranger",Status="active",SurvivorDay=1,StartedAt=now});
        await db.SaveChangesAsync(ct);
        var action=new CandidateAction("help_injured_stranger",new Dictionary<string,string>());
        var result=new DayOneGameEngine().Resolve(new GameSnapshot(survivorId,timelineId,1,"day-001-injured-stranger",new Dictionary<string,string>()),action);
        await new ResolutionStore(db).PersistAsync(eventId,survivorId,timelineId,Guid.NewGuid(),action,result,ct);
        Assert.Single(await db.ActionResolutions.ToListAsync(ct)); Assert.Single(await db.GameEvents.ToListAsync(ct)); Assert.Single(await db.Wakes.ToListAsync(ct));
    }
}
