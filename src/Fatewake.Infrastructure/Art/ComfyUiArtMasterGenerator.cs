using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
namespace Fatewake.Infrastructure.Art;
/// <summary>Generates master PNG artwork through ComfyUI, uploading approved continuity references before workflow submission.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Art/ComfyUiArtMasterGenerator.md">ComfyUiArtMasterGenerator documentation</see>.</remarks>
public sealed class ComfyUiArtMasterGenerator(HttpClient http,IOptions<ComfyUiArtGenerationOptions> configured,IArtBinaryStorage storage):IArtMasterGenerator
{
 public async Task<GeneratedArtMaster> GenerateAsync(ArtGenerationInvocation invocation,CancellationToken ct=default)
 {
  var o=configured.Value;if(!o.Enabled)throw new InvalidOperationException("ComfyUI art generation is not enabled.");
  var path=Path.GetFullPath(o.WorkflowPath);if(!File.Exists(path))throw new FileNotFoundException("ComfyUI API workflow was not found.",path);
  var template=await File.ReadAllTextAsync(path,ct);
  var workflow=template.Replace(o.PromptToken,JsonEncodedText.Encode(invocation.Request.ResolvedPrompt).ToString(),StringComparison.Ordinal)
   .Replace(o.NegativePromptToken,JsonEncodedText.Encode(invocation.Request.NegativeInstructions??string.Empty).ToString(),StringComparison.Ordinal)
   .Replace(o.SeedToken,StableSeed(invocation.IdempotencyKey).ToString(),StringComparison.Ordinal);
  if(invocation.Request.ReferenceImages is not null)
   foreach(var reference in invocation.Request.ReferenceImages)
   {
    var uploaded=await UploadReferenceAsync(reference,invocation.IdempotencyKey,ct);
    var token=$"{o.ReferenceTokenPrefix}{NormalizeRole(reference.Role)}__";
    workflow=workflow.Replace(token,JsonEncodedText.Encode(uploaded).ToString(),StringComparison.Ordinal);
   }
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
    foreach(var node in outputs.EnumerateObject())if(node.Value.TryGetProperty("images",out var images)&&images.GetArrayLength()>0)
    {
     var image=images[0];var filename=image.GetProperty("filename").GetString()!;var subfolder=image.TryGetProperty("subfolder",out var sf)?sf.GetString()??"":"";var type=image.TryGetProperty("type",out var ty)?ty.GetString()??"output":"output";
     var url=$"view?filename={Uri.EscapeDataString(filename)}&subfolder={Uri.EscapeDataString(subfolder)}&type={Uri.EscapeDataString(type)}";
     return new GeneratedArtMaster(await http.GetByteArrayAsync(url,timeout.Token),promptId,null);
    }
   await Task.Delay(o.PollIntervalMilliseconds,timeout.Token);
  }
 }
 private async Task<string> UploadReferenceAsync(ArtReferenceImage reference,string jobKey,CancellationToken ct)
 {
  await using var source=await storage.OpenReadAsync(reference.StorageKey,ct);using var ms=new MemoryStream();await source.CopyToAsync(ms,ct);
  using var form=new MultipartFormDataContent();var part=new ByteArrayContent(ms.ToArray());part.Headers.ContentType=new("image/png");
  var name=$"fatewake/{Sanitize(jobKey)}/{NormalizeRole(reference.Role)}-{Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(ms.ToArray())).ToLowerInvariant()[..16]}.png";
  form.Add(part,"image",Path.GetFileName(name));form.Add(new StringContent(Path.GetDirectoryName(name)!.Replace('\\','/')),"subfolder");form.Add(new StringContent("true"),"overwrite");
  using var response=await http.PostAsync("upload/image",form,ct);var body=await response.Content.ReadAsStringAsync(ct);
  if(!response.IsSuccessStatusCode)throw new HttpRequestException($"ComfyUI reference upload failed ({(int)response.StatusCode}): {body[..Math.Min(1000,body.Length)]}");
  using var json=JsonDocument.Parse(body);return json.RootElement.TryGetProperty("subfolder",out var sf)&&!string.IsNullOrWhiteSpace(sf.GetString())?$"{sf.GetString()}/{json.RootElement.GetProperty("name").GetString()}":json.RootElement.GetProperty("name").GetString()!;
 }
 private static string NormalizeRole(string role)=>new string(role.Trim().ToUpperInvariant().Select(c=>char.IsLetterOrDigit(c)?c:'_').ToArray());
 private static string Sanitize(string value)=>new string(value.Select(c=>char.IsLetterOrDigit(c)||c=='-'||c=='_'?c:'_').ToArray());
 private static long StableSeed(string key){var hash=System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key));return BitConverter.ToInt64(hash,0)&long.MaxValue;}
}