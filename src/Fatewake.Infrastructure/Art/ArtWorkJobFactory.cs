using System.Text.Json;
using Fatewake.Infrastructure.Work;

namespace Fatewake.Infrastructure.Art;
/// <summary>Builds and persists the distributed artwork pipeline with parallel derivative processing and a validation/finalization fan-in.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Art/ArtWorkJobFactory.md">ArtWorkJobFactory documentation</see>.</remarks>
public sealed class ArtWorkJobFactory(IWorkJobBuilder jobs) : IArtWorkJobFactory
{
    /// <inheritdoc/>
    public Task<CreatedWorkJob> CreateAsync(ArtWorkRequest request, CancellationToken ct = default)
        => CreateAsync(request, static (_, _) => Task.CompletedTask, ct);

    /// <inheritdoc />
    public Task<CreatedWorkJob> CreateAsync(ArtWorkRequest request,
        Func<Guid, CancellationToken, Task> transactionWork, CancellationToken ct = default)
    {
        if (request.IsPrivate && request.AccountId is null)
            throw new ArgumentException("Private artwork requires an owning account.", nameof(request));
        var input = JsonSerializer.Serialize(request);
        var steps = new WorkStepDefinition[]
        {
            new("resolve",ArtWorkStepTypes.Resolve,ArtWorkQueues.Generate,input,request.Priority),
            new("generate",ArtWorkStepTypes.GenerateMaster,ArtWorkQueues.Generate,input,request.Priority,3,["resolve"]),
            new("webp",ArtWorkStepTypes.EncodeWebP,ArtWorkQueues.Encode,input,request.Priority,5,["generate"]),
            new("png",ArtWorkStepTypes.OptimizePng,ArtWorkQueues.Encode,input,request.Priority,5,["generate"]),
            new("metadata",ArtWorkStepTypes.InspectMetadata,ArtWorkQueues.Encode,input,request.Priority,5,["generate"]),
            new("validate",ArtWorkStepTypes.Validate,ArtWorkQueues.Validate,input,request.Priority,3,["webp","png","metadata"]),
            new("finalize",ArtWorkStepTypes.Finalize,ArtWorkQueues.Finalize,input,request.Priority,3,["validate"])
        };
        var idempotencyKey = request.IsPrivate
            ? $"{request.VisualFingerprint}:account:{request.AccountId:N}"
            : request.VisualFingerprint;
        return jobs.CreateAsync(new("art.asset", idempotencyKey, input, steps, request.Priority), transactionWork, ct);
    }
}