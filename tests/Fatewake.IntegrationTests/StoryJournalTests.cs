using System.Text.Json;
using Fatewake.Infrastructure.Persistence;
using Fatewake.Infrastructure.Story;
using Microsoft.EntityFrameworkCore;
namespace Fatewake.IntegrationTests;
/// <summary>Checks durable chronology, artwork identity, and staged disclosure against PostgreSQL.</summary>
public sealed class StoryJournalTests
{
    /// <summary>History stays immutable and read-only; external causes and future impacts stay private.</summary>
    [Fact]
    public async Task History_pins_art_and_hides_external_causes_until_revealed()
    {
        var connection = Environment.GetEnvironmentVariable("FATEWAKE_STORY_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection))
            Assert.Skip("An isolated PostgreSQL story database is required.");
        var ct = TestContext.Current.CancellationToken;
        await using var db = new FatewakeDbContext(new DbContextOptionsBuilder<FatewakeDbContext>().UseNpgsql(connection).Options);
        await db.Database.MigrateAsync(ct);
        if (!await db.Realms.AnyAsync(x => x.Key == "the-silence", ct))
        {
            db.Realms.Add(new RealmRecord { Id = Guid.NewGuid(), Key = "the-silence", Name = "The Silence" });
            await db.SaveChangesAsync(ct);
        }
        var sessions = new SessionStore(db);
        var state = await sessions.StartOrResumeAsync(null, "test", ct);
        var story = new StoryJournalService(db);
        await story.RecordProgressAsync(state, ct);
        await story.RecordProgressAsync(state, ct);
        Assert.Single(await story.ReadAsync(state.SurvivorId, ct));
        foreach (var version in new[] { 1, 2 })
            db.ArtAssets.Add(new ArtAssetRecord
            {
                Id = Guid.NewGuid(),
                Key = "bg-day1-bedroom-dark",
                Version = version,
                AssetType = "background",
                Status = ArtAssetStatus.Approved,
                VisualFingerprint = "test-" + version,
                ContentHash = "test-" + version,
                MasterPngStorageKey = "test.png",
                CreatedAt = DateTimeOffset.UtcNow
            });
        await db.SaveChangesAsync(ct);
        Assert.True(await story.PinArtworkAsync(state.SurvivorId, state.EventInstanceId, state.BeatKey, new Dictionary<string, int?> { ["bg-day1-bedroom-dark"] = 1 }, ct));
        Assert.True(await story.PinArtworkAsync(state.SurvivorId, state.EventInstanceId, state.BeatKey, new Dictionary<string, int?> { ["bg-day1-bedroom-dark"] = 2 }, ct));
        Assert.False(await story.PinArtworkAsync(state.SurvivorId, state.EventInstanceId, state.BeatKey, new Dictionary<string, int?> { ["unknown"] = 1 }, ct));
        Assert.Equal(1, (await story.ReadAsync(state.SurvivorId, ct))[0].Layers.Single(x => x.AssetKey == "bg-day1-bedroom-dark").AssetVersion);
        Assert.Null(await sessions.SavePresentationProgressAsync(state.SurvivorId, state.EventInstanceId, state.SceneKey, "silence", ct));
        var next = await sessions.SavePresentationProgressAsync(state.SurvivorId, state.EventInstanceId, state.SceneKey, "house-flicker", ct);
        await story.RecordProgressAsync(next!, ct);
        var source = new GameEventRecord
        {
            Id = Guid.NewGuid(),
            TimelineId = state.TimelineId,
            EventType = "external_effect",
            Sequence = 1,
            SurvivorDay = 1,
            CorrelationId = Guid.NewGuid(),
            Payload = "{}",
            OccurredAt = DateTimeOffset.UtcNow
        };
        var future = new GameEventRecord
        {
            Id = Guid.NewGuid(),
            TimelineId = state.TimelineId,
            EventType = "external_effect",
            Sequence = 2,
            SurvivorDay = 1,
            CorrelationId = Guid.NewGuid(),
            Payload = "{}",
            OccurredAt = DateTimeOffset.UtcNow
        };
        db.GameEvents.AddRange(source, future);
        await db.SaveChangesAsync(ct);
        var cutscene = await story.RecordInfluenceAsync(state.SurvivorId, source.Id, "The group reaches shelter before the storm.", "Another survivor unlocked the gate.", 1, ct);
        Assert.Equal(cutscene, await story.RecordInfluenceAsync(state.SurvivorId, source.Id, "duplicate", null, 1, ct));
        await story.RecordInfluenceAsync(state.SurvivorId, future.Id, "FUTURE SPOILER", null, 2, ct);
        var before = await db.Set<StoryPanelRecord>().CountAsync(ct);
        var sequence = (await db.Timelines.SingleAsync(x => x.Id == state.TimelineId, ct)).NextEventSequence;
        var panels = await story.ReadAsync(state.SurvivorId, ct);
        Assert.Equal(3, panels.Length);
        Assert.Equal(new long[] { 1, 2, 3 }, panels.Select(x => x.Sequence));
        var json = JsonSerializer.Serialize(panels);
        Assert.Contains("reaches shelter", json);
        Assert.DoesNotContain("unlocked", json);
        Assert.DoesNotContain("FUTURE SPOILER", json);
        Assert.DoesNotContain("HiddenCause", json);
        Assert.DoesNotContain("SourceEvent", json);
        var notice = Assert.Single(await story.NoticesAsync(state.SurvivorId, ct));
        Assert.Equal("Your Thread has changed", notice.Title);
        Assert.DoesNotContain("unlocked", notice.Message);
        Assert.False(await story.AcknowledgeAsync(Guid.NewGuid(), cutscene, 1, ct));
        Assert.False(await story.AcknowledgeAsync(state.SurvivorId, cutscene, 2, ct));
        Assert.True(await story.AcknowledgeAsync(state.SurvivorId, cutscene, 1, ct));
        Assert.Empty(await story.NoticesAsync(state.SurvivorId, ct));
        Assert.Equal(before, await db.Set<StoryPanelRecord>().CountAsync(ct));
        Assert.Equal(sequence, (await db.Timelines.SingleAsync(x => x.Id == state.TimelineId, ct)).NextEventSequence);
        await story.RevealCauseAsync(cutscene, ct);
        await story.RevealCauseAsync(cutscene, ct);
        Assert.Contains("unlocked the gate", JsonSerializer.Serialize(await story.ReadAsync(state.SurvivorId, ct)));
        notice = Assert.Single(await story.NoticesAsync(state.SurvivorId, ct));
        Assert.Equal(2, notice.Revision);
        Assert.Equal("Your story is clearer", notice.Title);
        Assert.Contains("unlocked", notice.Message);
        Assert.True(await story.AcknowledgeAsync(state.SurvivorId, cutscene, 1, ct));
        Assert.Single(await story.NoticesAsync(state.SurvivorId, ct));
        Assert.True(await story.AcknowledgeAsync(state.SurvivorId, cutscene, 2, ct));
        Assert.Empty(await story.NoticesAsync(state.SurvivorId, ct));
    }
}
