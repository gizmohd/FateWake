using System.Text.Json;
using Fatewake.Infrastructure.Persistence;
using Fatewake.Infrastructure.Work;
namespace Fatewake.Infrastructure.Art;
/// <summary>Reuses an approved master or generates and durably stores exactly one master PNG for an artwork job.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Art/ArtGenerateMasterWorkStepHandler.md">ArtGenerateMasterWorkStepHandler documentation</see>.</remarks>
public sealed class ArtGenerateMasterWorkStepHandler(IWorkArtifactStore artifacts,IArtBinaryStorage storage,IArtMasterGenerator generator):IWorkStepHandler
{
    public string StepType=>ArtWorkStepTypes.GenerateMaster;
    public async Task<string?> ExecuteAsync(WorkStepExecutionContext context,CancellationToken ct)
    {
        var existingMaster=await artifacts.GetAsync(context.JobId,"art.master",ct);
        if(existingMaster is not null)return existingMaster;

        var request=JsonSerializer.Deserialize<ArtWorkRequest>(context.Input)??throw new InvalidOperationException("Invalid art work request.");
        var resolveJson=await artifacts.GetAsync(context.JobId,"art.resolve",ct)??throw new InvalidOperationException("Resolve artifact is required before generation.");
        var resolve=JsonSerializer.Deserialize<ArtResolveResult>(resolveJson)??throw new InvalidOperationException("Invalid resolve artifact.");
        if(resolve.Reused)
        {
            if(string.IsNullOrWhiteSpace(resolve.MasterPngStorageKey))throw new InvalidOperationException("Reused asset has no master PNG storage key.");
            var reused=new ArtMasterArtifact(resolve.MasterPngStorageKey,true,resolve.AssetId,request.Provider,request.Model,null,0m);
            var reusedJson=JsonSerializer.Serialize(reused);await artifacts.PutAsync(context.JobId,"art.master",reusedJson,ct);return reusedJson;
        }

        var generated=await generator.GenerateAsync(request,ct);
        var storageKey=$"art/staging/{context.JobId:N}/master.png";
        await using(var stream=new MemoryStream(generated.MasterPng,writable:false))
            await storage.PutAsync(storageKey,stream,"image/png",ct);
        var master=new ArtMasterArtifact(storageKey,false,null,request.Provider,request.Model,generated.ProviderJobId,generated.CostUsd);
        var output=JsonSerializer.Serialize(master);await artifacts.PutAsync(context.JobId,"art.master",output,ct);return output;
    }
}