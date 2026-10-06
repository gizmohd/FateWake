using System.Text.Json; using Fatewake.Infrastructure.Persistence; using Fatewake.Infrastructure.Work;
namespace Fatewake.Infrastructure.Art;
/// <summary>Inspects and hashes the durable master PNG independently of derivative encoding.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Art/ArtInspectMetadataWorkStepHandler.md">ArtInspectMetadataWorkStepHandler documentation</see>.</remarks>
public sealed class ArtInspectMetadataWorkStepHandler(IWorkArtifactStore artifacts,IArtBinaryStorage storage,IArtImageProcessor images):IWorkStepHandler
{
 public string StepType=>ArtWorkStepTypes.InspectMetadata;
 public async Task<string?> ExecuteAsync(WorkStepExecutionContext context,CancellationToken ct){var old=await artifacts.GetAsync(context.JobId,"art.metadata",ct);if(old is not null)return old;var master=JsonSerializer.Deserialize<ArtMasterArtifact>(await artifacts.GetAsync(context.JobId,"art.master",ct)??throw new InvalidOperationException("Master artifact required."))!;var bytes=await ArtWorkStepUtilities.ReadAllAsync(storage,master.StorageKey,ct);var info=images.InspectPng(bytes);var result=new ArtMetadataArtifact(ArtWorkStepUtilities.Hash(bytes),info.Width,info.Height,info.HasAlpha,bytes.LongLength);var json=JsonSerializer.Serialize(result);await artifacts.PutAsync(context.JobId,"art.metadata",json,ct);return json;}
}