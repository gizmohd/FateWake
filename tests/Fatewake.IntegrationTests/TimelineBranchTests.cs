using System.Text.Json;
using System.Diagnostics;
using System.Collections.Concurrent;
using Fatewake.Observability;
using Fatewake.GameEngine;
using Fatewake.GameEngine.DayOne;
using Fatewake.Infrastructure.Persistence;
using Fatewake.Infrastructure.Story;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using OpenIddict.EntityFrameworkCore.Models;
using Fatewake.Api;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;

namespace Fatewake.IntegrationTests;

/// <summary>Exercises canonical personal-branch isolation, rollback, idempotency and concurrent selection against PostgreSQL.</summary>
/// <see href="../../docs/code/tests/Fatewake.IntegrationTests/TimelineBranchTests.md">Documentation</see>
public sealed class TimelineBranchTests
{
    /// <summary>Verifies a fork changes the choice without rewriting its source and each branch resumes independently.</summary>
    [Fact]
    public async Task Fork_changes_choice_without_rewriting_source_and_switch_resumes_each_branch()
    {
        var fixture = await CreateAsync();
        await using var db = fixture.Db;
        var ct = TestContext.Current.CancellationToken;
        var activities = new ConcurrentBag<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == OperationTelemetry.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => activities.Add(activity)
        };
        ActivitySource.AddActivityListener(listener);
        var original = fixture.Session;
        var sessions = new SessionStore(db);
        foreach (var beat in new[] { "house-flicker", "street-reveal", "first-choice" })
            await sessions.SavePresentationProgressAsync(original.SurvivorId, original.EventInstanceId, original.SceneKey, beat, ct);
        var checkpoint = await db.Set<TimelineCheckpointRecord>().SingleAsync(row => row.Key == "first-choice", ct);
        var sourceAction = new CandidateAction("help_injured_stranger", new Dictionary<string, string>());
        var engine = new DayOneGameEngine();
        await new ResolutionStore(db).PersistAsync(original.EventInstanceId, original.SurvivorId, original.TimelineId,
            Guid.NewGuid(), sourceAction, engine.Resolve(new(original.SurvivorId, original.TimelineId, 1, original.EventKey, new Dictionary<string, string>()), sourceAction), ct);
        var parentPanel = await db.Set<StoryPanelRecord>().SingleAsync(row => row.TimelineId == original.TimelineId && row.Sequence == 1, ct);
        parentPanel.HiddenCauseNarration = "FUTURE-PARENT-ONLY";
        parentPanel.CauseRevealed = true;
        await db.SaveChangesAsync(ct);
        var branches = new TimelineBranchService(db, NullLogger<TimelineBranchService>.Instance);
        var request = Guid.NewGuid();
        var child = await branches.ForkAsync(fixture.Account, original.SurvivorId, original.TimelineId, checkpoint.Id, request, ct);
        Assert.Equal(original.SurvivorId, child.SurvivorId);
        Assert.NotEqual(original.TimelineId, child.TimelineId);
        Assert.Equal("first-choice", child.BeatKey);
        Assert.Null(child.LastOutcome);
        Assert.Equal(child.TimelineId, (await branches.ForkAsync(fixture.Account, original.SurvivorId,
            original.TimelineId, checkpoint.Id, request, ct)).TimelineId);
        Assert.Equal(2, await db.Set<TimelineBranchRecord>().CountAsync(ct));
        Assert.Empty(await db.Wakes.Where(row => row.TimelineId == child.TimelineId).ToArrayAsync(ct));
        Assert.DoesNotContain("FUTURE-PARENT-ONLY", JsonSerializer.Serialize(await new StoryJournalService(db).ReadAsync(child.SurvivorId, ct)));
        var childAction = new CandidateAction("stay_inside", new Dictionary<string, string>());
        var childKey = Guid.NewGuid();
        var childOutcome = engine.Resolve(new(child.SurvivorId, child.TimelineId, 1, child.EventKey, new Dictionary<string, string>()), childAction);
        var resolutions = new ResolutionStore(db);
        await resolutions.PersistAsync(child.EventInstanceId, child.SurvivorId, child.TimelineId, childKey, childAction, childOutcome, ct);
        var replay = await resolutions.PersistAsync(child.EventInstanceId, child.SurvivorId, child.TimelineId, childKey, childAction, childOutcome, ct);
        Assert.Equal(childOutcome.OutcomeKey, replay.OutcomeKey);
        Assert.Single(replay.Wakes);
        await Assert.ThrowsAsync<TimelineConflictException>(() => resolutions.PersistAsync(child.EventInstanceId,
            child.SurvivorId, child.TimelineId, childKey, sourceAction, childOutcome, ct));
        await Assert.ThrowsAsync<TimelineConflictException>(() => resolutions.PersistAsync(child.EventInstanceId,
            child.SurvivorId, child.TimelineId, Guid.NewGuid(), sourceAction, childOutcome, ct));
        var facts = await new TimelineStateStore(db).LoadAsync(child.SurvivorId, child.TimelineId, child.EventInstanceId, ct);
        Assert.Equal("decreased", facts.Facts["michelle.trust"]);
        Assert.False(facts.Facts.ContainsKey("player.exposed_self_to_danger"));
        var restored = await branches.SelectAsync(fixture.Account, original.SurvivorId, child.TimelineId, original.TimelineId, ct);
        Assert.Equal("injured_stranger_helped", restored.LastOutcome);
        Assert.Contains("FUTURE-PARENT-ONLY", JsonSerializer.Serialize(await new StoryJournalService(db).ReadAsync(original.SurvivorId, ct)));
        await branches.SelectAsync(fixture.Account, original.SurvivorId, original.TimelineId, child.TimelineId, ct);
        var resumed = await sessions.StartOrResumeAsync(null, "ignored", ct, fixture.Account);
        Assert.Equal(child.TimelineId, resumed.TimelineId);
        Assert.Equal("remained_inside", resumed.LastOutcome);
        Assert.Equal(2, await db.GameEvents.CountAsync(ct));
        Assert.Equal(2, await db.Wakes.CountAsync(ct));
        var childCheckpoint = await db.Set<TimelineCheckpointRecord>()
            .SingleAsync(row => row.TimelineId == child.TimelineId && row.Key == "first-choice", ct);
        var grandchild = await branches.ForkAsync(fixture.Account, child.SurvivorId, child.TimelineId, childCheckpoint.Id, Guid.NewGuid(), ct);
        Assert.Null(grandchild.LastOutcome);
        Assert.Equal(original.TimelineId, (await db.Set<TimelineBranchRecord>()
            .SingleAsync(row => row.TimelineId == grandchild.TimelineId, ct)).RootTimelineId);
        Assert.Contains(activities, activity => activity.OperationName == "timeline.fork");
        Assert.Contains(activities, activity => activity.OperationName == "timeline.switch");
        Assert.All(activities.Where(activity => activity.OperationName.StartsWith("timeline.", StringComparison.Ordinal)),
            activity => Assert.All(activity.TagObjects, tag => Assert.True(tag.Key is "operation.outcome" or "error.type")));
        Assert.DoesNotContain("FUTURE-PARENT-ONLY", JsonSerializer.Serialize(activities.Select(activity => activity.TagObjects)));
    }

    /// <summary>Verifies fresh starts are distinct and stale, foreign, or shared-dependency requests are rejected.</summary>
    [Fact]
    public async Task Fresh_start_is_distinct_and_stale_tabs_foreign_owners_and_shared_dependencies_are_rejected()
    {
        var fixture = await CreateAsync();
        await using var db = fixture.Db;
        var ct = TestContext.Current.CancellationToken;
        var original = fixture.Session;
        var branches = new TimelineBranchService(db, NullLogger<TimelineBranchService>.Instance);
        await Assert.ThrowsAsync<TimelineConflictException>(() => branches.ForkAsync(fixture.Account,
            original.SurvivorId, original.TimelineId, null, Guid.NewGuid(), ct, 99));
        var fresh = await branches.ForkAsync(fixture.Account, original.SurvivorId, original.TimelineId, null, Guid.NewGuid(), ct);
        Assert.Equal("phone-0617", fresh.BeatKey);
        Assert.Equal("fresh-day1", (await db.Set<TimelineBranchRecord>().SingleAsync(row => row.TimelineId == fresh.TimelineId, ct)).Origin);
        await Assert.ThrowsAsync<TimelineConflictException>(() => new SessionStore(db).SavePresentationProgressAsync(
            original.SurvivorId, original.EventInstanceId, original.SceneKey, "house-flicker", ct));
        await Assert.ThrowsAsync<TimelineConflictException>(() => branches.ForkAsync(fixture.Account,
            original.SurvivorId, original.TimelineId, null, Guid.NewGuid(), ct));
        var foreign = new AccountRecord { Id = Guid.NewGuid(), Status = AccountStatus.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        db.Add(foreign);
        await db.SaveChangesAsync(ct);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => branches.ForkAsync(foreign.Id,
            original.SurvivorId, fresh.TimelineId, null, Guid.NewGuid(), ct));
        var timeline = await db.Timelines.SingleAsync(row => row.Id == fresh.TimelineId, ct);
        timeline.ConvergenceState = "shared";
        await db.SaveChangesAsync(ct);
        await Assert.ThrowsAsync<TimelineConflictException>(() => branches.ForkAsync(fixture.Account,
            original.SurvivorId, fresh.TimelineId, null, Guid.NewGuid(), ct));
        Assert.Equal(2, await db.Set<TimelineBranchRecord>().CountAsync(ct));
    }

    /// <summary>Verifies a failed resolution rolls back its checkpoint, journal, and effects.</summary>
    [Fact]
    public async Task Resolution_failure_rolls_back_checkpoint_journal_and_effects()
    {
        var fixture = await CreateAsync();
        await using var db = fixture.Db;
        var ct = TestContext.Current.CancellationToken;
        var activities = new ConcurrentBag<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == OperationTelemetry.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => activities.Add(activity)
        };
        ActivitySource.AddActivityListener(listener);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION reject_test_wake() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                RAISE EXCEPTION 'injected timeline persistence failure';
            END $$;
            CREATE TRIGGER reject_test_wake BEFORE INSERT ON wake FOR EACH ROW EXECUTE FUNCTION reject_test_wake();
            """, ct);
        var state = fixture.Session;
        var action = new CandidateAction("help_injured_stranger", new Dictionary<string, string>());
        var outcome = new DayOneGameEngine().Resolve(new(state.SurvivorId, state.TimelineId, 1, state.EventKey, new Dictionary<string, string>()), action);
        await Assert.ThrowsAsync<DbUpdateException>(() => new ResolutionStore(db).PersistAsync(state.EventInstanceId,
            state.SurvivorId, state.TimelineId, Guid.NewGuid(), action, outcome, ct));
        db.ChangeTracker.Clear();
        Assert.Empty(await db.GameEvents.ToArrayAsync(ct));
        Assert.Empty(await db.ActionAttempts.ToArrayAsync(ct));
        Assert.Single(await db.Set<TimelineCheckpointRecord>().ToArrayAsync(ct));
        Assert.Single(await db.Set<StoryPanelRecord>().ToArrayAsync(ct));
        var resumed = await new SessionStore(db).StartOrResumeAsync(state.SurvivorId, "ignored", ct, fixture.Account);
        Assert.Equal("phone-0617", resumed.BeatKey);
        Assert.Null(resumed.LastOutcome);
        Assert.Contains(activities, activity => activity.OperationName == "game.persist_resolution" &&
            activity.Status == ActivityStatusCode.Error && (string?)activity.GetTagItem("operation.outcome") == "error");
    }

    /// <summary>Verifies destructive development reset works independently but never removes preserved descendant history.</summary>
    [Fact]
    public async Task Development_reset_clears_own_chapter_but_refuses_preserved_descendants()
    {
        var fixture = await CreateAsync();
        await using var db = fixture.Db;
        var ct = TestContext.Current.CancellationToken;
        var state = fixture.Session;
        var action = new CandidateAction("help_injured_stranger", new Dictionary<string, string>());
        var outcome = new DayOneGameEngine().Resolve(new(state.SurvivorId, state.TimelineId, 1, state.EventKey, new Dictionary<string, string>()), action);
        await new ResolutionStore(db).PersistAsync(state.EventInstanceId, state.SurvivorId, state.TimelineId, Guid.NewGuid(), action, outcome, ct);
        var reset = await new SessionStore(db).ResetChapterAsync(state.SurvivorId, state.EventInstanceId, ct);
        Assert.Equal("phone-0617", reset!.BeatKey);
        Assert.Null(reset.LastOutcome);
        Assert.Empty(await db.Wakes.ToArrayAsync(ct));
        Assert.Empty(await db.ActionAttempts.ToArrayAsync(ct));
        Assert.Single(await db.Set<StoryPanelRecord>().ToArrayAsync(ct));
        Assert.Single(await db.Set<TimelineCheckpointRecord>().ToArrayAsync(ct));
        var branches = new TimelineBranchService(db, NullLogger<TimelineBranchService>.Instance);
        var child = await branches.ForkAsync(fixture.Account, state.SurvivorId, state.TimelineId, null, Guid.NewGuid(), ct);
        await branches.SelectAsync(fixture.Account, state.SurvivorId, child.TimelineId, state.TimelineId, ct);
        await Assert.ThrowsAsync<TimelineConflictException>(() => new SessionStore(db).ResetChapterAsync(state.SurvivorId, state.EventInstanceId, ct));
        Assert.Equal(2, await db.Set<TimelineBranchRecord>().CountAsync(ct));
    }

    /// <summary>Verifies an insert failure cannot strand a new event, projection or selected-timeline pointer.</summary>
    [Fact]
    public async Task Fork_failure_rolls_back_all_child_rows_and_selected_pointer()
    {
        var fixture = await CreateAsync();
        await using var db = fixture.Db;
        var ct = TestContext.Current.CancellationToken;
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION reject_test_branch() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                RAISE EXCEPTION 'injected branch persistence failure';
            END $$;
            CREATE TRIGGER reject_test_branch BEFORE INSERT ON timeline_branch FOR EACH ROW EXECUTE FUNCTION reject_test_branch();
            """, ct);
        var state = fixture.Session;
        await Assert.ThrowsAsync<DbUpdateException>(() => new TimelineBranchService(db, NullLogger<TimelineBranchService>.Instance)
            .ForkAsync(fixture.Account, state.SurvivorId, state.TimelineId, null, Guid.NewGuid(), ct));
        db.ChangeTracker.Clear();
        Assert.Equal(state.TimelineId, (await db.Survivors.SingleAsync(ct)).TimelineId);
        Assert.Single(await db.Timelines.ToArrayAsync(ct));
        Assert.Single(await db.EventInstances.ToArrayAsync(ct));
        Assert.Single(await db.Set<SurvivorTimelineStateRecord>().ToArrayAsync(ct));
        Assert.Single(await db.Set<TimelineCheckpointRecord>().ToArrayAsync(ct));
        Assert.Single(await db.Set<TimelineBranchRecord>().ToArrayAsync(ct));
    }

    /// <summary>Verifies a concurrent progression/fork checks expected event version inside the common lock.</summary>
    [Fact]
    public async Task Progress_and_versioned_fork_race_accepts_only_one_mutation()
    {
        var fixture = await CreateAsync();
        await using var db = fixture.Db;
        var state = fixture.Session;
        var ct = TestContext.Current.CancellationToken;
        async Task<bool> Mutate(bool fork)
        {
            await using var other = new FatewakeDbContext(new DbContextOptionsBuilder<FatewakeDbContext>().UseNpgsql(Connection()).Options);
            try
            {
                if (fork)
                    await new TimelineBranchService(other, NullLogger<TimelineBranchService>.Instance)
                        .ForkAsync(fixture.Account, state.SurvivorId, state.TimelineId, null, Guid.NewGuid(), ct, state.Version);
                else
                    await new SessionStore(other).SavePresentationProgressAsync(state.SurvivorId, state.EventInstanceId, state.SceneKey, "house-flicker", ct);
                return true;
            }
            catch (TimelineConflictException)
            {
                return false;
            }
        }
        var results = await Task.WhenAll(Mutate(false), Mutate(true));
        Assert.Single(results, success => success);
        db.ChangeTracker.Clear();
        Assert.Equal(results[1] ? 2 : 1, await db.Set<TimelineBranchRecord>().CountAsync(ct));
        var resumed = await new SessionStore(db).StartOrResumeAsync(null, "test", ct, fixture.Account);
        Assert.Equal(results[1] ? "phone-0617" : "house-flicker", resumed.BeatKey);
    }

    /// <summary>Verifies the lock refreshes tracked episodes/facts after another scope has committed progression or effects.</summary>
    [Fact]
    public async Task Reused_context_checks_fresh_versions_progress_and_canonical_facts_under_the_lock()
    {
        var fixture = await CreateAsync();
        await using var db = fixture.Db;
        var ct = TestContext.Current.CancellationToken;
        var state = fixture.Session;
        await using var other = new FatewakeDbContext(new DbContextOptionsBuilder<FatewakeDbContext>().UseNpgsql(Connection()).Options);
        await new SessionStore(other).SavePresentationProgressAsync(state.SurvivorId, state.EventInstanceId, state.SceneKey, "house-flicker", ct);
        await Assert.ThrowsAsync<TimelineConflictException>(() => new TimelineBranchService(db, NullLogger<TimelineBranchService>.Instance)
            .ForkAsync(fixture.Account, state.SurvivorId, state.TimelineId, null, Guid.NewGuid(), ct, state.Version));
        var progressed = await new SessionStore(db).SavePresentationProgressAsync(state.SurvivorId, state.EventInstanceId, state.SceneKey, "street-reveal", ct);
        Assert.Equal("street-reveal", progressed!.BeatKey);
        var action = new CandidateAction("help_injured_stranger", new Dictionary<string, string>());
        var outcome = new DayOneGameEngine().Resolve(new(state.SurvivorId, state.TimelineId, 1, state.EventKey, new Dictionary<string, string>()), action);
        await new ResolutionStore(other).PersistAsync(state.EventInstanceId, state.SurvivorId, state.TimelineId, Guid.NewGuid(), action, outcome, ct);
        var canonical = await new TimelineStateStore(db).LoadAsync(state.SurvivorId, state.TimelineId, state.EventInstanceId, ct);
        Assert.Equal("increased", canonical.Facts["michelle.trust"]);
    }

    /// <summary>Verifies upgrading a genuine existing decision preserves history and reconstructs only its current authoritative projection.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Migration_preserves_existing_save_and_backfills_facts_without_fabricated_checkpoints(bool shared)
    {
        var fixture = await CreateAsync();
        await using var db = fixture.Db;
        var ct = TestContext.Current.CancellationToken;
        var state = fixture.Session;
        var action = new CandidateAction("help_injured_stranger", new Dictionary<string, string>());
        var outcome = new DayOneGameEngine().Resolve(new(state.SurvivorId, state.TimelineId, 1, state.EventKey, new Dictionary<string, string>()), action);
        await new ResolutionStore(db).PersistAsync(state.EventInstanceId, state.SurvivorId, state.TimelineId, Guid.NewGuid(), action, outcome, ct);
        db.Set<OpenIddictEntityFrameworkCoreApplication>().AddRange(
            new()
            {
                Id = "web-test",
                ClientId = "fatewake-web",
                DisplayName = "Fatewake Web"
            },
            new()
            {
                Id = "api-test",
                ClientId = "fatewake-api",
                DisplayName = shared ? "Custom API name" : "Fatewake API"
            });
        db.Set<OpenIddictEntityFrameworkCoreScope>().Add(new()
        {
            Id = "scope-test",
            Name = "fatewake-api",
            DisplayName = "Fatewake API"
        });
        await db.SaveChangesAsync(ct);
        if (shared)
        {
            db.Survivors.Add(new SurvivorRecord
            {
                Id = Guid.NewGuid(),
                AccountId = fixture.Account,
                TimelineId = state.TimelineId,
                DisplayName = "Other Pathweaver",
                IdentityMode = "undetermined",
                BroadRegion = "test",
                SurvivorDay = 1,
                Status = SurvivorStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync(ct);
        }
        var previousMigration = db.Database.GetMigrations().TakeWhile(id => id != "20261119100000_TimelineBranches").Last();
        await db.GetService<IMigrator>().MigrateAsync(previousMigration, ct);
        db.ChangeTracker.Clear();
        await db.Database.MigrateAsync(ct);
        Assert.Equal("Pathweaver Web", (await db.Set<OpenIddictEntityFrameworkCoreApplication>().SingleAsync(row => row.ClientId == "fatewake-web", ct)).DisplayName);
        Assert.Equal(shared ? "Custom API name" : "Pathweaver API",
            (await db.Set<OpenIddictEntityFrameworkCoreApplication>().SingleAsync(row => row.ClientId == "fatewake-api", ct)).DisplayName);
        Assert.Equal("Pathweaver API", (await db.Set<OpenIddictEntityFrameworkCoreScope>().SingleAsync(row => row.Name == "fatewake-api", ct)).DisplayName);
        Assert.Equal(fixture.Account, (await db.Survivors.SingleAsync(row => row.Id == state.SurvivorId, ct)).AccountId);
        Assert.Equal(shared ? 0 : 1, await db.Set<TimelineBranchRecord>().CountAsync(ct));
        Assert.Equal(!shared, await new TimelineStateStore(db).EligibleAsync(state.TimelineId, ct));
        Assert.Single(await db.GameEvents.ToArrayAsync(ct));
        Assert.Single(await db.Wakes.ToArrayAsync(ct));
        Assert.Empty(await db.Set<TimelineCheckpointRecord>().ToArrayAsync(ct));
        var canonical = await new TimelineStateStore(db).LoadAsync(state.SurvivorId, state.TimelineId, state.EventInstanceId, ct);
        Assert.Equal("increased", canonical.Facts["michelle.trust"]);
        var resumed = await new SessionStore(db).StartOrResumeAsync(state.SurvivorId, "test", ct, fixture.Account);
        Assert.Equal("injured_stranger_helped", resumed.LastOutcome);
        Assert.Equal(state.TimelineId, resumed.TimelineId);
        Assert.Empty(await db.Set<TimelineCheckpointRecord>().ToArrayAsync(ct));
    }

    /// <summary>Verifies valid envelope hashes cannot conceal malformed canonical state.</summary>
    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("not-json")]
    public void Snapshot_rejects_malformed_facts_even_with_a_matching_integrity_hash(string facts)
    {
        var snapshot = new TimelineSnapshot(1, TimelineSnapshotCodec.RulesVersion, TimelineSnapshotCodec.ContentVersion,
            1, SurvivorStatus.Active, facts, new EventInstanceRecord
            {
                Id = Guid.NewGuid(),
                SurvivorId = Guid.NewGuid(),
                TimelineId = Guid.NewGuid(),
                SurvivorDay = 1,
                Status = EventInstanceStatus.Active,
                EventKey = "day-001-injured-stranger",
                State = """{"sceneKey":"day1-0617-0643","beatKey":"phone-0617"}"""
            }, [], [], null);
        var json = JsonSerializer.Serialize(snapshot);
        Assert.Throws<TimelineConflictException>(() => TimelineSnapshotCodec.Read(json, TimelineSnapshotCodec.Hash(json)));
    }

    /// <summary>Verifies concurrent forks from one selection create exactly one child branch.</summary>
    [Fact]
    public async Task Concurrent_forks_from_the_same_selection_create_only_one_child()
    {
        var fixture = await CreateAsync();
        await using var db = fixture.Db;
        var connection = Connection();
        var state = fixture.Session;
        async Task<bool> Fork()
        {
            await using var other = new FatewakeDbContext(new DbContextOptionsBuilder<FatewakeDbContext>().UseNpgsql(connection).Options);
            try
            {
                await new TimelineBranchService(other, NullLogger<TimelineBranchService>.Instance)
                    .ForkAsync(fixture.Account, state.SurvivorId, state.TimelineId, null, Guid.NewGuid(), TestContext.Current.CancellationToken);
                return true;
            }
            catch (TimelineConflictException)
            {
                return false;
            }
        }
        var results = await Task.WhenAll(Fork(), Fork());
        Assert.Single(results, value => value);
        Assert.Equal(2, await db.Set<TimelineBranchRecord>().CountAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>Verifies snapshot integrity ignores JSONB property order and rejects unsupported versions.</summary>
    [Fact]
    public void Snapshot_integrity_is_independent_of_jsonb_property_order_and_rejects_unsupported_versions()
    {
        Assert.Equal(TimelineSnapshotCodec.Hash("""{"b":2,"a":1}"""), TimelineSnapshotCodec.Hash("""{ "a": 1, "b": 2 }"""));
        const string unsupported = """{"SchemaVersion":99}""";
        Assert.Throws<TimelineConflictException>(() => TimelineSnapshotCodec.Read(unsupported, TimelineSnapshotCodec.Hash(unsupported)));
        Assert.Throws<TimelineConflictException>(() => TimelineSnapshotCodec.Read(unsupported, new string('0', 64)));
    }

    /// <summary>Verifies HTTP routes enforce account ownership and never expose raw checkpoints.</summary>
    [Fact]
    public async Task Http_routes_enforce_authenticated_ownership_and_never_return_raw_checkpoints()
    {
        var fixture = await CreateAsync();
        await using var db = fixture.Db;
        var ct = TestContext.Current.CancellationToken;
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddDbContext<FatewakeDbContext>(options => options.UseNpgsql(Connection()));
        builder.Services.AddAuthentication("timeline-test").AddScheme<AuthenticationSchemeOptions, TimelineTestAuthenticationHandler>(
            "timeline-test", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddScoped<TimelineBranchService>();
        builder.Services.AddRateLimiter(options =>
            options.AddFixedWindowLimiter("timeline-mutation", limiter =>
            {
                limiter.PermitLimit = 6;
                limiter.Window = TimeSpan.FromMinutes(1);
            }));
        await using var app = builder.Build();
        app.Use(async (context, next) =>
        {
            try
            {
                await next(context);
            }
            catch (TimelineConflictException ex)
            {
                context.Response.StatusCode = 409;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = ex.Message
                }, ct);
            }
        });
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
        app.MapTimelines();
        await app.StartAsync(ct);
        using var client = app.GetTestClient();
        var path = $"/api/timelines/{fixture.Session.SurvivorId}";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path, ct)).StatusCode);
        client.DefaultRequestHeaders.Add("X-Test-Account", Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path, ct)).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Test-Account");
        client.DefaultRequestHeaders.Add("X-Test-Account", fixture.Account.ToString());
        var threads = await client.GetStringAsync(path, ct);
        Assert.Contains("6:17 AM", threads);
        Assert.DoesNotContain("narrativeFacts", threads, StringComparison.OrdinalIgnoreCase);
        var listing = await client.GetStringAsync(path + "/checkpoints", ct);
        Assert.Contains("opening", listing);
        Assert.DoesNotContain("snapshot", listing, StringComparison.OrdinalIgnoreCase);
        client.DefaultRequestHeaders.Add("Authorization", "Bearer invalid");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path, ct)).StatusCode);
        client.DefaultRequestHeaders.Remove("Authorization");
        using var missingVersion = await client.PostAsJsonAsync(path + "/fork",
            new TimelineMutationRequest(fixture.Session.TimelineId, Guid.NewGuid()), ct);
        Assert.Equal(HttpStatusCode.BadRequest, missingVersion.StatusCode);
        using var unknownCheckpoint = await client.PostAsJsonAsync(path + "/fork",
            new TimelineMutationRequest(fixture.Session.TimelineId, Guid.NewGuid(), Guid.NewGuid(), ExpectedVersion: 0), ct);
        Assert.Equal(HttpStatusCode.Conflict, unknownCheckpoint.StatusCode);
        using var fresh = await client.PostAsJsonAsync(path + "/fork",
            new TimelineMutationRequest(fixture.Session.TimelineId, Guid.NewGuid(), ExpectedVersion: 0), ct);
        Assert.Equal(HttpStatusCode.OK, fresh.StatusCode);
        var child = await fresh.Content.ReadFromJsonAsync<SessionState>(ct);
        Assert.NotEqual(fixture.Session.TimelineId, child!.TimelineId);
        Assert.Equal(fixture.Session.SurvivorId, child.SurvivorId);
        using var stale = await client.PostAsJsonAsync(path + "/fork",
            new TimelineMutationRequest(fixture.Session.TimelineId, Guid.NewGuid(), ExpectedVersion: 0), ct);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
    }

    private static string Connection()
    {
        var connection = Environment.GetEnvironmentVariable("FATEWAKE_TIMELINE_TEST_CONNECTION");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(connection), "Requires a disposable FATEWAKE_TIMELINE_TEST_CONNECTION database.");
        Assert.Contains("timeline", new NpgsqlConnectionStringBuilder(connection).Database!, StringComparison.OrdinalIgnoreCase);
        return connection;
    }

    private static async Task<(FatewakeDbContext Db, Guid Account, SessionState Session)> CreateAsync()
    {
        var db = new FatewakeDbContext(new DbContextOptionsBuilder<FatewakeDbContext>().UseNpgsql(Connection()).Options);
        var ct = TestContext.Current.CancellationToken;
        await db.Database.EnsureDeletedAsync(ct);
        await DatabaseInitializer.InitializeAsync(db, ct);
        var account = new AccountRecord
        {
            Id = Guid.NewGuid(),
            Status = AccountStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Add(account);
        await db.SaveChangesAsync(ct);
        return (db, account.Id, await new SessionStore(db).StartOrResumeAsync(null, "test", ct, account.Id));
    }
}
