using Fatewake.Infrastructure.Art;
using Fatewake.Infrastructure.Work;

namespace Fatewake.IntegrationTests;

/// <summary>Verifies artwork job identities and bounded priority across shared and account-private requests.</summary>
/// <see href="../../../docs/code/tests/Fatewake.IntegrationTests/ArtWorkJobFactoryTests.md">ArtWorkJobFactoryTests documentation</see>
public sealed class ArtWorkJobFactoryTests
{
    /// <summary>Private requests are account-scoped and require an owner; shared requests retain the canonical fingerprint key.</summary>
    [Fact]
    public async Task Private_job_identity_is_account_scoped()
    {
        var builder = new CapturingBuilder();
        var factory = new ArtWorkJobFactory(builder);
        var ct = TestContext.Current.CancellationToken;
        const string fingerprint = "canonical-fingerprint";
        await factory.CreateAsync(new ArtWorkRequest("shared", "Scene", fingerprint, "prompt", "None", "None"), ct);
        Assert.Equal(fingerprint, builder.Definition!.IdempotencyKey);

        var firstAccount = Guid.NewGuid();
        await factory.CreateAsync(new ArtWorkRequest("private-a", "Character", fingerprint, "prompt", "None", "None",
            AccountId: firstAccount, IsPrivate: true), ct);
        var firstKey = builder.Definition!.IdempotencyKey;
        await factory.CreateAsync(new ArtWorkRequest("private-b", "Character", fingerprint, "prompt", "None", "None",
            AccountId: Guid.NewGuid(), IsPrivate: true), ct);
        Assert.NotEqual(firstKey, builder.Definition!.IdempotencyKey);
        Assert.StartsWith(fingerprint + ":account:", firstKey);

        await Assert.ThrowsAsync<ArgumentException>(() => factory.CreateAsync(
            new ArtWorkRequest("private", "Character", fingerprint, "prompt", "None", "None", IsPrivate: true), ct));
    }

    private sealed class CapturingBuilder : IWorkJobBuilder
    {
        public WorkJobDefinition? Definition { get; private set; }

        public Task<CreatedWorkJob> CreateAsync(WorkJobDefinition definition, CancellationToken ct = default)
        {
            Definition = definition;
            return Task.FromResult(new CreatedWorkJob(Guid.NewGuid(), false, new Dictionary<string, Guid>()));
        }
    }
}
