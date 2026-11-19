using System.Text.Json;
using Fatewake.Infrastructure.Work;

namespace Fatewake.Infrastructure.Art;

/// <summary>Creates coalesced durable authoring jobs with identity state committed atomically.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Art/ArtPromptJobFactory.md">Documentation</see>.</remarks>
public sealed class ArtPromptJobFactory(IWorkJobBuilder jobs)
{
    /// <summary>Queues one prompt identity and commits its pending state with the durable work record.</summary>
    public Task<CreatedWorkJob> CreateAsync(ArtPromptAuthoringRequest request,
        Func<Guid, CancellationToken, Task> transactionWork, CancellationToken ct = default)
    {
        var input = JsonSerializer.Serialize(request);
        var step = new WorkStepDefinition("author", ArtPromptWorkStepTypes.Author, ArtWorkQueues.Prompt,
            input, 0, 3);
        return jobs.CreateAsync(new WorkJobDefinition("art.prompt", request.IdentityKey, input, [step]),
            transactionWork, ct);
    }
}
