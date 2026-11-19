using System.Text.Json;
using Fatewake.GameEngine;
using Fatewake.GameEngine.DayOne;
using Fatewake.Infrastructure.Authentication;
using Fatewake.Infrastructure.Persistence;
using Fatewake.Infrastructure.Rag;
using Fatewake.Infrastructure.Story;
using Fatewake.Infrastructure.Work;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fatewake.IntegrationTests;

/// <summary>Verifies that scene continuations reuse safe cues without sharing another player's input.</summary>
public sealed class StoryBranchTests
{
    /// <summary>Similar Day One actions reuse a compatible cue and keep the two player inputs separate.</summary>
    [Fact]
    public async Task Similar_actions_reuse_only_the_approved_branch_cue()
    {
        var connection = Environment.GetEnvironmentVariable("FATEWAKE_REGISTRATION_TEST_CONNECTION");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(connection), "An isolated PostgreSQL database is required.");
        var ct = TestContext.Current.CancellationToken;
        await using var db = new FatewakeDbContext(new DbContextOptionsBuilder<FatewakeDbContext>().UseNpgsql(connection).Options);
        await DatabaseInitializer.InitializeAsync(db, ct);

        var author = new RecordingAuthor();
        var worker = new SceneWorkStepHandler(db, new Canon(), author,
            new SceneAuthoringOptions { Enabled = true, ApiKey = "test-key" }, NullLogger<SceneWorkStepHandler>.Instance);

        var first = await GenerateAsync("investigate_radio", "Tune the radio.");
        var second = await GenerateAsync("investigate_radio", "Check the broadcast.");
        var different = await GenerateAsync("stay_inside", "Wait inside.");

        Assert.Null(author.Contexts[0].BranchFocus);
        Assert.Equal("listen_for_updates", author.Contexts[1].BranchFocus);
        Assert.Equal("Check the broadcast.", author.Contexts[1].PlayerChoice);
        Assert.DoesNotContain(author.Contexts[1].RecentSummaries, text => text.Contains("Tune the radio."));
        Assert.Null(author.Contexts[2].BranchFocus);
        Assert.NotNull(first.BranchKey);
        Assert.Equal(first.BranchKey, second.BranchKey);
        Assert.NotEqual(first.BranchKey, different.BranchKey);
        Assert.Equal(ContentLevel.Under14, second.ContentLevel);
        Assert.Contains("freeform", second.Spec);

        async Task<GeneratedSceneRecord> GenerateAsync(string actionType, string input)
        {
            var session = await new SessionStore(db).StartOrResumeAsync(null, "unknown", ct);
            var action = new CandidateAction(actionType, new Dictionary<string, string>(), input);
            var snapshot = new GameSnapshot(session.SurvivorId, session.TimelineId, session.SurvivorDay,
                session.EventKey, new Dictionary<string, string>());
            await new ResolutionStore(db).PersistAsync(session.EventInstanceId, session.SurvivorId,
                session.TimelineId, Guid.NewGuid(), action, new DayOneGameEngine().Resolve(snapshot, action), ct);
            var now = DateTimeOffset.UtcNow;
            var scene = new GeneratedSceneRecord
            {
                Id = Guid.NewGuid(), SurvivorId = session.SurvivorId, TimelineId = session.TimelineId,
                Sequence = 1, Status = GeneratedSceneStatus.Pending, CreatedAt = now, UpdatedAt = now
            };
            db.Add(scene);
            await db.SaveChangesAsync(ct);
            var work = new WorkStepExecutionContext(Guid.NewGuid(), Guid.NewGuid(),
                JsonSerializer.Serialize(new SceneGenerationRequest(scene.Id)), 1);
            await worker.ExecuteAsync(work, ct);
            return scene;
        }
    }

    /// <summary>Empty retrieval produces one private, reviewed continuity passage per ready scene.</summary>
    [Fact]
    public async Task Empty_corpus_continues_and_persists_scoped_canon()
    {
        var connection = Environment.GetEnvironmentVariable("FATEWAKE_REGISTRATION_TEST_CONNECTION");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(connection), "An isolated PostgreSQL database is required.");
        var ct = TestContext.Current.CancellationToken;
        await using var db = new FatewakeDbContext(new DbContextOptionsBuilder<FatewakeDbContext>().UseNpgsql(connection).Options);
        await DatabaseInitializer.InitializeAsync(db, ct);
        var owner = await new SessionStore(db).StartOrResumeAsync(null, "unknown", ct);
        var other = await new SessionStore(db).StartOrResumeAsync(null, "unknown", ct);
        var author = new RecordingAuthor();
        var worker = new SceneWorkStepHandler(db, new EmptyCorpus(), author,
            new SceneAuthoringOptions { Enabled = true, ApiKey = "test-key" }, NullLogger<SceneWorkStepHandler>.Instance);

        var first = await GenerateAsync(owner, 1);
        Assert.Empty(author.Contexts[0].CanonPassages);
        Assert.Equal(GeneratedSceneStatus.Ready, first.Status);
        Assert.NotNull(first.RagSourceId);
        var source = await db.RagSources.SingleAsync(x => x.Id == first.RagSourceId, ct);
        Assert.Equal("GeneratedThreadCanon", source.SourceType);
        Assert.Equal("Private", source.Visibility);
        var document = await db.RagDocuments.SingleAsync(x => x.SourceId == source.Id, ct);
        var passage = await db.RagChunks.SingleAsync(x => x.DocumentId == document.Id, ct);
        Assert.Equal(first.Summary, passage.Text);
        Assert.Equal(SceneWork.TimelineKey(owner.TimelineId), passage.TimelineKey);
        Assert.Equal(owner.SurvivorDay, passage.SurvivorDayMin);

        await GenerateAsync(owner, 2);
        Assert.Equal(first.Summary, Assert.Single(author.Contexts[1].CanonPassages));
        await GenerateAsync(other, 1);
        Assert.Empty(author.Contexts[2].CanonPassages);

        var second = await db.Set<GeneratedSceneRecord>().SingleAsync(x => x.SurvivorId == owner.SurvivorId && x.Sequence == 2, ct);
        first.ContentLevel = ContentLevel.AdultLanguage;
        second.ContentLevel = ContentLevel.AdultLanguage;
        await db.SaveChangesAsync(ct);
        await GenerateAsync(owner, 3);
        Assert.Empty(author.Contexts[3].CanonPassages);

        async Task<GeneratedSceneRecord> GenerateAsync(SessionState session, int sequence)
        {
            var scene = new GeneratedSceneRecord
            {
                Id = Guid.NewGuid(), SurvivorId = session.SurvivorId, TimelineId = session.TimelineId,
                Sequence = sequence, Status = GeneratedSceneStatus.Pending, CreatedAt = DateTimeOffset.UtcNow
            };
            db.Add(scene);
            await db.SaveChangesAsync(ct);
            var work = new WorkStepExecutionContext(Guid.NewGuid(), Guid.NewGuid(),
                JsonSerializer.Serialize(new SceneGenerationRequest(scene.Id)), 1);
            await worker.ExecuteAsync(work, ct);
            await worker.ExecuteAsync(work with { Attempt = 2 }, ct);
            return scene;
        }
    }

    private sealed class EmptyCorpus : IRagRetriever
    {
        public Task<IReadOnlyList<RagPassage>> SearchAsync(string query, RagSearchFilter filter, int limit, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<RagPassage>>([]);
    }

    private sealed class RecordingAuthor : ISceneAuthor
    {
        public List<SceneAuthoringContext> Contexts { get; } = [];

        public Task<SceneSpec> AuthorAsync(SceneAuthoringContext context, Guid workJobId, int attempt, CancellationToken ct)
        {
            Contexts.Add(context);
            return Task.FromResult(new SceneSpec("The signal", "The street grows quiet.", "6:45 AM",
                [new("Narration", "The street grows quiet.", null, null, "background", "A quiet street")],
                [new("look_around", "Look around", false)], "listen_for_updates"));
        }
    }

    private sealed class Canon : IRagRetriever
    {
        public Task<IReadOnlyList<RagPassage>> SearchAsync(string query, RagSearchFilter filter, int limit, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<RagPassage>>([new() { Text = "The story begins in a quiet neighborhood." }]);
    }
}
