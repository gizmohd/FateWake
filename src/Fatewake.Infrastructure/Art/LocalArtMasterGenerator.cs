using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
namespace Fatewake.Infrastructure.Art;
/// <summary>Generates master artwork through a local OpenAI-compatible image endpoint.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Art/LocalArtMasterGenerator.md">LocalArtMasterGenerator documentation</see>.</remarks>
public sealed class LocalArtMasterGenerator(HttpClient http,IOptions<LocalArtGenerationOptions> options):IArtMasterGenerator
{
 public async Task<GeneratedArtMaster> GenerateAsync(ArtGenerationInvocation invocation,CancellationToken ct=default)
 {
  var o=options.Value;if(!o.Enabled)throw new InvalidOperationException("Local art generation is not enabled.");
  using var req=new HttpRequestMessage(HttpMethod.Post,"images/generations");
  if(!string.IsNullOrWhiteSpace(o.ApiKey))req.Headers.Authorization=new AuthenticationHeaderValue("Bearer",o.ApiKey);
  req.Headers.TryAddWithoutValidation("Idempotency-Key",invocation.IdempotencyKey);
  var prompt=string.IsNullOrWhiteSpace(invocation.Request.NegativeInstructions)?invocation.Request.ResolvedPrompt:$"{invocation.Request.ResolvedPrompt}\n\nAvoid: {invocation.Request.NegativeInstructions}";
  req.Content=JsonContent.Create(new{model=string.IsNullOrWhiteSpace(invocation.Request.Model)?o.Model:invocation.Request.Model,prompt,size=o.Size,quality=o.Quality,response_format="b64_json"});
  using var res=await http.SendAsync(req,HttpCompletionOption.ResponseHeadersRead,ct);var body=await res.Content.ReadAsStringAsync(ct);
  if(!res.IsSuccessStatusCode)throw new HttpRequestException($"Local image generation failed ({(int)res.StatusCode}): {body[..Math.Min(body.Length,1000)]}");
  using var json=JsonDocument.Parse(body);var first=json.RootElement.GetProperty("data")[0];
  var b64=first.GetProperty("b64_json").GetString()??throw new InvalidDataException("Local endpoint returned empty image data.");
  var id=res.Headers.TryGetValues("x-request-id",out var ids)?ids.FirstOrDefault():null;
  return new GeneratedArtMaster(Convert.FromBase64String(b64),id,null);
 }
}