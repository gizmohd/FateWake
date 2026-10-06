using System.Text.Json;
using Fatewake.Infrastructure.Persistence;
using Fatewake.Infrastructure.Work;
namespace Fatewake.Infrastructure.Art;
/// <summary>Performs the exact approved-asset lookup that gates all artwork provider generation.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Art/ArtResolveWorkStepHandler.md">ArtResolveWorkStepHandler documentation</see>.</remarks>
public sealed class ArtResolveWorkStepHandler(IArtAssetStore assets,IWorkArtifactStore artifacts):IWorkStepHandler
{
    public string StepType=>ArtWorkStepTypes.Resolve;
    public async Task<string?> ExecuteAsync(WorkStepExecutionContext context,CancellationToken ct)
    {
        var request=JsonSerializer.Deserialize<ArtWorkRequest>(context.Input)??throw new InvalidOperationException("Invalid art work request.");
        var match=await assets.FindApprovedByFingerprintAsync(request.VisualFingerprint,ct);
        var output=JsonSerializer.Serialize(new ArtResolveResult(match is not null,match?.Id,match?.MasterPngStorageKey));
        await artifacts.PutAsync(context.JobId,"art.resolve",output,ct);
        return output;
    }
}