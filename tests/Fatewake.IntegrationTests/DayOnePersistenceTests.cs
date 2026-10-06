using System.Text.Json;
using Fatewake.GameEngine;
using Fatewake.GameEngine.DayOne;
using Fatewake.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

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
        db.Survivors.Add(new SurvivorRecord{Id=survivorId,TimelineId=timelineId,DisplayName="Test Survivor",IdentityMode="fictional",BroadRegion="test",Status=SurvivorStatus.Active,CreatedAt=now,UpdatedAt=now});
        db.EventInstances.Add(new EventInstanceRecord{Id=eventId,TimelineId=timelineId,SurvivorId=survivorId,EventKey="day-001-injured-stranger",Status=EventInstanceStatus.Active,SurvivorDay=1,StartedAt=now});
        await db.SaveChangesAsync(ct);
        var action=new CandidateAction("help_injured_stranger",new Dictionary<string,string>());
        var result=new DayOneGameEngine().Resolve(new GameSnapshot(survivorId,timelineId,1,"day-001-injured-stranger",new Dictionary<string,string>()),action);
        await new ResolutionStore(db).PersistAsync(eventId,survivorId,timelineId,Guid.NewGuid(),action,result,ct);
        Assert.Single(await db.ActionResolutions.ToListAsync(ct)); Assert.Single(await db.GameEvents.ToListAsync(ct)); Assert.Single(await db.Wakes.ToListAsync(ct));
    }

    [Fact]
    public async Task Status_migration_preserves_existing_data_and_can_roll_back()
    {
        var cs = Environment.GetEnvironmentVariable("FATEWAKE_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(cs)) return;
        var ct = TestContext.Current.CancellationToken;
        var options = new DbContextOptionsBuilder<FatewakeDbContext>().UseNpgsql(cs).Options;
        await using var db = new FatewakeDbContext(options);
        await db.Database.EnsureDeletedAsync(ct);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261006000100_Initial",ct);

        var accountId = Guid.NewGuid();
        var realmId = Guid.NewGuid();
        var timelineId = Guid.NewGuid();
        var survivorId = Guid.NewGuid();
        var episodeId = Guid.NewGuid();
        var gameEventId = Guid.NewGuid();
        var wakeId = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($$"""
INSERT INTO account ("Id", "Status", "CreatedAt", "UpdatedAt")
VALUES ({{accountId}}, 'active', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP);
INSERT INTO realm ("Id", "Key", "Name")
VALUES ({{realmId}}, 'the-silence', 'The Silence');
INSERT INTO timeline ("Id", "RealmId", "ProgressionState", "ConvergenceState", "WorldClockPolicy", "CreatedAt")
VALUES ({{timelineId}}, {{realmId}}, 'personal', 'isolated', 'activity', CURRENT_TIMESTAMP);
INSERT INTO survivor ("Id", "TimelineId", "DisplayName", "IdentityMode", "BroadRegion", "Status", "CreatedAt", "UpdatedAt")
VALUES ({{survivorId}}, {{timelineId}}, 'Test Survivor', 'fictional', 'test', 'active', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP);
INSERT INTO event_instance ("Id", "TimelineId", "SurvivorId", "EventKey", "Status", "SurvivorDay", "StartedAt")
VALUES ({{episodeId}}, {{timelineId}}, {{survivorId}}, 'day-001-injured-stranger', 'active', 1, CURRENT_TIMESTAMP);
INSERT INTO game_event ("Id", "TimelineId", "EventType", "Sequence", "SurvivorDay", "CorrelationId", "Payload", "OccurredAt")
VALUES ({{gameEventId}}, {{timelineId}}, 'action_resolved', 1, 1, {{gameEventId}}, '{}', CURRENT_TIMESTAMP);
INSERT INTO wake ("Id", "TimelineId", "OriginEventId", "WakeType", "Scope", "Severity", "State", "Properties", "CreatedDay", "CreatedAt")
VALUES ({{wakeId}}, {{timelineId}}, {{gameEventId}}, 'test', 'personal', 1, 'active', '{}', 1, CURRENT_TIMESTAMP);
""",ct);

        await migrator.MigrateAsync(cancellationToken:ct);
        Assert.Equal(AccountStatus.Active,(await db.Accounts.SingleAsync(ct)).Status);
        Assert.Equal(SurvivorStatus.Active,(await db.Survivors.SingleAsync(ct)).Status);
        Assert.Equal(EventInstanceStatus.Active,(await db.EventInstances.SingleAsync(ct)).Status);
        Assert.Equal(WakeState.Active,(await db.Wakes.SingleAsync(ct)).State);
        var columnTypes = await db.Database.SqlQueryRaw<string>("""
SELECT data_type AS "Value" FROM information_schema.columns
WHERE table_schema = 'public' AND
((table_name IN ('account', 'survivor', 'event_instance') AND column_name = 'Status')
 OR (table_name = 'wake' AND column_name = 'State'))
""").ToListAsync(ct);
        Assert.Equal(4,columnTypes.Count);
        Assert.All(columnTypes,type=>Assert.Equal("integer",type));
        Assert.Equal(1,await db.Database.SqlQueryRaw<int>("""
SELECT count(*)::integer AS "Value" FROM pg_indexes
WHERE schemaname = 'public' AND indexname = 'IX_event_instance_SurvivorId_Status_SurvivorDay'
""").SingleAsync(ct));

        var session = await new SessionStore(db).StartOrResumeAsync(survivorId,"test",ct);
        Assert.Equal(episodeId,session.EventInstanceId);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(session,new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.Equal("active",json.RootElement.GetProperty("status").GetString());

        await migrator.MigrateAsync("20261006000100_Initial",ct);
        var legacyStatuses = await db.Database.SqlQueryRaw<string>("""
SELECT "Status" AS "Value" FROM account
UNION ALL SELECT "Status" FROM survivor
UNION ALL SELECT "Status" FROM event_instance
UNION ALL SELECT "State" FROM wake
""").ToListAsync(ct);
        Assert.Equal(4,legacyStatuses.Count);
        Assert.All(legacyStatuses,status=>Assert.Equal("active",status));
        await migrator.MigrateAsync(cancellationToken:ct);
    }

    [Fact]
    public async Task Status_migration_rejects_unsupported_values_without_losing_data()
    {
        var cs = Environment.GetEnvironmentVariable("FATEWAKE_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(cs)) return;
        var ct = TestContext.Current.CancellationToken;
        var options = new DbContextOptionsBuilder<FatewakeDbContext>().UseNpgsql(cs).Options;
        await using var db = new FatewakeDbContext(options);
        await db.Database.EnsureDeletedAsync(ct);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261006000100_Initial",ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
INSERT INTO account ("Id", "Status", "CreatedAt", "UpdatedAt")
VALUES ({Guid.NewGuid()}, 'unsupported', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
""",ct);

        var error = await Assert.ThrowsAsync<PostgresException>(()=>migrator.MigrateAsync(cancellationToken:ct));
        Assert.Equal(PostgresErrorCodes.NotNullViolation,error.SqlState);
        Assert.Equal("unsupported",await db.Database.SqlQueryRaw<string>("""SELECT "Status" AS "Value" FROM account""").SingleAsync(ct));
        Assert.Single(await db.Database.GetAppliedMigrationsAsync(ct));
    }
}
