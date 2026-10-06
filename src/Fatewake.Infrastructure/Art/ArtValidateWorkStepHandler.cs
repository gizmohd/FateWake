using System.Text.Json;
using Fatewake.Infrastructure.Persistence;
using Fatewake.Infrastructure.Work;
namespace Fatewake.Infrastructure.Art;
/// <summary>Validates durable master and delivery artifacts before they may enter the approved asset library.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Art/ArtValidateWorkStepHandler.md">ArtValidateWorkStepHandler documentation</see>.</remarks>
public sealed class ArtValidateWorkStepHandler(IWorkArtifactStore artifacts,IArtBinaryStorage storage):IWorkStepHandler
{
 public string StepType=>ArtWorkStepTypes.Validate;
 public async Task<string?> ExecuteAsync(WorkStepExecutionContext context,CancellationToken ct)
 {
  var old=await artifacts.GetAsync(context.JobId,"art.validation",ct);if(old is not null)return old;
  async Task<string> Required(string key)=>await artifacts.GetAsync(context.JobId,key,ct)??throw new InvalidOperationException($"Required artifact {key} is missing.");
  var master=JsonSerializer.Deserialize<ArtMasterArtifact>(await Required("art.master"))!;
  var metadata=JsonSerializer.Deserialize<ArtMetadataArtifact>(await Required("art.metadata"))!;
  var webp=JsonSerializer.Deserialize<ArtDerivativeArtifact>(await Required("art.webp"))!;
  var png=JsonSerializer.Deserialize<ArtDerivativeArtifact>(await Required("art.png"))!;
  string? error=null;
  if(metadata.Width<=0||metadata.Height<=0)error="Master dimensions are invalid.";
  else if(webp.Width!=metadata.Width||webp.Height!=metadata.Height||png.Width!=metadata.Width||png.Height!=metadata.Height)error="Delivery dimensions do not match master.";
  else if(!await storage.ExistsAsync(master.StorageKey,ct)||!await storage.ExistsAsync(webp.StorageKey,ct)||!await storage.ExistsAsync(png.StorageKey,ct))error="One or more required binaries are missing.";
  var result=new ArtValidationArtifact(error is null,error);var json=JsonSerializer.Serialize(result);await artifacts.PutAsync(context.JobId,"art.validation",json,ct);
  if(error is not null)throw new InvalidOperationException(error);return json;
 }
}