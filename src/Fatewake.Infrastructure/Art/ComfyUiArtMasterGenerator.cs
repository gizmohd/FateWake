using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
namespace Fatewake.Infrastructure.Art;
/// <summary>Generates master PNG artwork by submitting a configurable API workflow to ComfyUI.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Art/ComfyUiArtMasterGenerator.md">ComfyUiArtMasterGenerator documentation</see>.</remarks>
public sealed class ComfyUiArtMasterGenerator(HttpClient http,IOptions<ComfyUiArtGenerationOptions> configured):IArtMasterGenerator
{
 public async Task<GeneratedArtMaster> GenerateAsync(ArtGenerationInvocation invocation,CancellationToken ct=default)
 {
  var o=configured.Value;if(!o.Enabled)throw new InvalidOperationException("ComfyUI art generation is not enabled.");
  var path=Path.GetFullPath(o.WorkflowPath);if(!File.Exists(path))throw new FileNotFoundException("ComfyUI API workflow was not found.",path);
  var template=await File.ReadAllTextAsync(path,ct);
  var seed=StableSeed(invocation.IdempotencyKey);
  var workflow=template.Replace(o.PromptToken,JsonEncodedText.Encode(invocation.Request.ResolvedPrompt).ToString(),StringComparison.Ordinal)
   .Replace(o.NegativePromptToken,JsonEncodedText.Encode(invocation.Request.NegativeInstructions??string.Empty).ToString(),StringComparison.Ordinal)
   .Replace(o.SeedToken,seed.ToString(),StringComparison.Ordinal);
  using var doc=JsonDocument.Parse(workflow);
  using var submit=await http.PostAsJsonAsync("prompt",new{prompt=doc.RootElement.Clone(),client_id=invocation.IdempotencyKey},ct);
  var submitBody=await submit.Content.ReadAsStringAsync(ct);if(!submit.IsSuccessStatusCode)throw new HttpRequestException($"ComfyUI submission failed ({(int)submit.StatusCode}): {submitBody[..Math.Min(1000,submitBody.Length)]}");
  using var submitted=JsonDocument.Parse(submitBody);var promptId=submitted.RootElement.GetProperty("prompt_id").GetString()??throw new InvalidDataException("ComfyUI returned no prompt_id.");
  using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(o.TimeoutSeconds));
  while(true)
  {
   timeout.Token.ThrowIfCancellationRequested();using var history=await http.GetAsync($"history/{Uri.EscapeDataString(promptId)}",timeout.Token);
   var body=await history.Content.ReadAsStringAsync(timeout.Token);history.EnsureSuccessStatusCode();using var json=JsonDocument.Parse(body);
   if(json.RootElement.TryGetProperty(promptId,out var entry)&&entry.TryGetProperty("outputs",out var outputs))
   {
    foreach(var node in outputs.EnumerateObject())if(node.Value.TryGetProperty("images",out var images)&&images.GetArrayLength()>0)
    {
     var image=images[0];var filename=image.GetProperty("filename").GetString()!;var subfolder=image.TryGetProperty("subfolder",out var sf)?sf.GetString()??"":"";var type=image.TryGetProperty("type",out var ty)?ty.GetString()??"output":"output";
     var url=$"view?filename={Uri.EscapeDataString(filename)}&subfolder={Uri.EscapeDataString(subfolder)}&type={Uri.EscapeDataString(type)}";
     var bytes=await http.GetByteArrayAsync(url,timeout.Token);return new GeneratedArtMaster(bytes,promptId,null);
    }
   }
   await Task.Delay(o.PollIntervalMilliseconds,timeout.Token);
  }
 }
 private static long StableSeed(string key){var hash=System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key));return Math.Abs(BitConverter.ToInt64(hash,0));}
}