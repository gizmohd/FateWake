using System.Text.Json;
using Fatewake.Infrastructure.Persistence;
using Fatewake.Infrastructure.Work;
namespace Fatewake.Infrastructure.Art;
/// <summary>Performs the exact approved-asset lookup that gates all artwork provider generation.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Art/ArtResolveWorkStepHandler.md">ArtResolveWorkStepHandler documentation</see>.</remarks>
public sealed class ArtResolveWorkStepHandler(IArtAssetStore assets):IWorkStepHandler
{
    public string StepType=>ArtWorkStepTypes.Resolve;
    public async Task<string?> ExecuteAsync(string input,CancellationToken ct)
    {
        var request=JsonSerializer.Deserialize<ArtWorkRequest>(input)??throw new InvalidOperationException("Invalid art work request.");
        var match=await assets.FindApprovedByFingerprintAsync(request.VisualFingerprint,ct);
        return JsonSerializer.Serialize(new ArtResolveResult(match is not null,match?.Id,match?.MasterPngStorageKey));
    }
}