using System.Text.Json;
using Fatewake.Infrastructure.Work;

namespace Fatewake.Infrastructure.Art;
/// <summary>Builds and persists the distributed artwork pipeline with parallel derivative processing and a validation/finalization fan-in.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Art/ArtWorkJobFactory.md">ArtWorkJobFactory documentation</see>.</remarks>
public sealed class ArtWorkJobFactory(IWorkJobBuilder jobs):IArtWorkJobFactory
{
    /// <inheritdoc/>
    public Task<CreatedWorkJob> CreateAsync(ArtWorkRequest request,CancellationToken ct=default)
    {
        var input=JsonSerializer.Serialize(request);
        var steps=new WorkStepDefinition[]
        {
            new("resolve",ArtWorkStepTypes.Resolve,ArtWorkQueues.Generate,input,request.Priority),
            new("generate",ArtWorkStepTypes.GenerateMaster,ArtWorkQueues.Generate,input,request.Priority,3,["resolve"]),
            new("webp",ArtWorkStepTypes.EncodeWebP,ArtWorkQueues.Encode,input,request.Priority,5,["generate"]),
            new("png",ArtWorkStepTypes.OptimizePng,ArtWorkQueues.Encode,input,request.Priority,5,["generate"]),
            new("metadata",ArtWorkStepTypes.InspectMetadata,ArtWorkQueues.Encode,input,request.Priority,5,["generate"]),
            new("validate",ArtWorkStepTypes.Validate,ArtWorkQueues.Validate,input,request.Priority,3,["webp","png","metadata"]),
            new("finalize",ArtWorkStepTypes.Finalize,ArtWorkQueues.Finalize,input,request.Priority,3,["validate"])
        };
        return jobs.CreateAsync(new("art.asset",request.VisualFingerprint,input,steps,request.Priority),ct);
    }
}