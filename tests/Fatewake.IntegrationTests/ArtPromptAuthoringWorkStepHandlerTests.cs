using System.Text.Json;
using Fatewake.Infrastructure.Art;
using Fatewake.Infrastructure.Persistence;
using Fatewake.Infrastructure.Work;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fatewake.IntegrationTests;

/// <summary>Verifies captured authoring context and idempotent successor creation after a durable save.</summary>
/// <see href="../../docs/code/tests/Fatewake.IntegrationTests/ArtPromptAuthoringWorkStepHandlerTests.md">Documentation</see>
public sealed class ArtPromptAuthoringWorkStepHandlerTests
{
    /// <summary>Uses the queued context and recovers after persistence without authoring or queueing twice.</summary>
    [Fact]
    public async Task Frozen_context_creates_one_pinned_successor_and_recovery_reuses_it()
    {
        var options = new DbContextOptionsBuilder<FatewakeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using var db = new FatewakeDbContext(options);
        const string identity = "bg-day1-bedroom-dark:shared:test";
        const string contextFingerprint = "captured-context-v1";
        var prompt = new ArtPromptRecord
        {
            Id = Guid.NewGuid(),
            IdentityKey = identity,
            AssetKey = "bg-day1-bedroom-dark",
            ContextFingerprint = contextFingerprint,
            StyleBibleVersion = "3",
            Status = "pending",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Add(prompt);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var ownerId = Guid.NewGuid();
        var survivorId = Guid.NewGuid();
        var captured = new ArtPromptAuthoringRequest(identity, prompt.AssetKey, "Background",
            contextFingerprint, "3", "Captured canonical facts at queue time.",
            "Opaque portrait environment.", "Captured shared style.", "Captured exclusions.", ownerId, survivorId);
        var author = new CapturingAuthor();
        var imageJobs = new CapturingImageJobs(db);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ArtGeneration:Provider"] = "None"
        }).Build();
        var handler = new ArtPromptAuthoringWorkStepHandler(db, author, imageJobs,
            new ArtPromptAuthoringOptions { Enabled = true, ApiKey = "fake-key" },
            configuration, NullLogger<ArtPromptAuthoringWorkStepHandler>.Instance);
        var jobId = Guid.NewGuid();
        var serializedInput = JsonSerializer.Serialize(captured);
        var execution = new WorkStepExecutionContext(jobId, Guid.NewGuid(), serializedInput, 1);

        var firstOutput = await handler.ExecuteAsync(execution, CancellationToken.None);

        Assert.Equal(captured, author.Request);
        const string futureThreadFacts = "A later Thread reveals the hidden cause.";
        Assert.DoesNotContain(futureThreadFacts, captured.CanonicalContext, StringComparison.Ordinal);
        Assert.DoesNotContain(futureThreadFacts, author.Request!.CanonicalContext, StringComparison.Ordinal);
        Assert.Equal(1, author.Calls);
        Assert.Equal(1, imageJobs.Calls);
        var imageRequest = imageJobs.Request
            ?? throw new InvalidOperationException("The prompt authoring workflow did not create an image request.");
        Assert.Equal("Captured shared style.\nEnforced role: environment only; no people, characters, faces or bodies\nOpaque portrait environment.\nSubject details: A quiet lived-in bedroom.\nCaptured canonical facts at queue time.",
            imageRequest.ResolvedPrompt);
        Assert.Contains("No new people.", imageRequest.NegativeInstructions);
        Assert.Equal(1, imageRequest.PromptRevisionNumber);

        var storedRevision = await db.Set<ArtPromptRevisionRecord>().AsNoTracking()
            .SingleAsync(row => row.ArtPromptId == prompt.Id, cancellationToken: TestContext.Current.CancellationToken);
        var storedPrompt = await db.Set<ArtPromptRecord>().AsNoTracking().SingleAsync(row => row.Id == prompt.Id, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("ai", storedRevision.Source);
        Assert.Equal(contextFingerprint, storedRevision.RequestContextFingerprint);
        Assert.Equal(storedRevision.Id, imageRequest.PromptRevisionId);
        Assert.Equal(storedRevision.Id, storedPrompt.CurrentRevisionId);
        Assert.Equal(storedRevision.Id, storedPrompt.LatestRevisionId);
        Assert.Equal("ready", JsonDocument.Parse(firstOutput!).RootElement.GetProperty("status").GetString());

        var recoveryOutput = await handler.ExecuteAsync(execution with
        {
            StepId = Guid.NewGuid(),
            Attempt = 2
        }, CancellationToken.None);

        Assert.Equal("ready", JsonDocument.Parse(recoveryOutput!).RootElement.GetProperty("status").GetString());
        Assert.Equal(1, author.Calls);
        Assert.Equal(1, imageJobs.Calls);
        Assert.Equal(1, await db.Set<ArtPromptRevisionRecord>().CountAsync(row => row.ArtPromptId == prompt.Id, cancellationToken: TestContext.Current.CancellationToken));
    }

    private sealed class CapturingAuthor : IArtPromptAuthor
    {
        public int Calls
        {
            get; private set;
        }

        public ArtPromptAuthoringRequest? Request
        {
            get; private set;
        }

        public Task<ArtPromptAuthoringResult> AuthorAsync(ArtPromptAuthoringRequest request, Guid workJobId,
            int attempt, CancellationToken ct)
        {
            Calls++;
            Request = request;
            return Task.FromResult(new ArtPromptAuthoringResult("A quiet lived-in bedroom.", "No new people.",
                "fake-request", 10, 5));
        }
    }

    private sealed class CapturingImageJobs(FatewakeDbContext db) : IArtWorkJobFactory
    {
        public int Calls
        {
            get; private set;
        }

        public ArtWorkRequest? Request
        {
            get; private set;
        }

        public Task<CreatedWorkJob> CreateAsync(ArtWorkRequest request, CancellationToken ct = default) =>
            throw new NotSupportedException("The authoring handler must persist its prompt and successor atomically.");

        public async Task<CreatedWorkJob> CreateAsync(ArtWorkRequest request,
            Func<Guid, CancellationToken, Task> transactionWork, CancellationToken ct = default)
        {
            Calls++;
            Request = request;
            var jobId = Guid.NewGuid();
            await transactionWork(jobId, ct);
            await db.SaveChangesAsync(ct);
            return new CreatedWorkJob(jobId, false, new Dictionary<string, Guid>());
        }
    }
}
