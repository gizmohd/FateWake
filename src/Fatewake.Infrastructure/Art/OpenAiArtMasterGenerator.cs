using Fatewake.Infrastructure.Usage;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Fatewake.Observability;
using Microsoft.Extensions.Logging;
namespace Fatewake.Infrastructure.Art;
/// <summary>Generates canonical PNG masters with the OpenAI Image API, optionally using approved reference images.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Art/OpenAiArtMasterGenerator.md">OpenAiArtMasterGenerator documentation</see>.</remarks>
public sealed class OpenAiArtMasterGenerator(HttpClient http, IOptions<OpenAiArtGenerationOptions> configured, IArtBinaryStorage storage, ILogger<OpenAiArtMasterGenerator>? log = null, IAiUsageRecorder? usage = null) : IArtMasterGenerator
{
    public Task<GeneratedArtMaster> GenerateAsync(ArtGenerationInvocation invocation, CancellationToken ct = default)
       => TrackedAiCall.RunAsync(usage, new AiCallContext(invocation.Request.Provider, invocation.Request.Model,
           (invocation.Request.ReferenceImages?.Count ?? 0) > 0 ? "image.edit" : "image.generate",
           $"{invocation.IdempotencyKey}:attempt:{invocation.Attempt}", invocation.WorkJobId, invocation.Request.AccountId, invocation.Request.SurvivorId),
           id => GenerateCoreAsync(invocation, id, ct), ct);
    private async Task<GeneratedArtMaster> GenerateCoreAsync(ArtGenerationInvocation invocation, Guid? usageId, CancellationToken ct)
    {
        using var operation = OperationTelemetry.Start("art.generate.openai", log);
        var o = configured.Value; if (!o.Enabled || string.IsNullOrWhiteSpace(o.ApiKey)) throw new InvalidOperationException("OpenAI art generation is not configured.");
        var background = invocation.Request.AssetType is "CharacterPose" or "CharacterExpression" or "Prop" or "Foreground" or "Lighting" or "Effect" or "Atmosphere" ? "transparent" : o.Background;
        using var request = new HttpRequestMessage(HttpMethod.Post, (invocation.Request.ReferenceImages?.Count ?? 0) > 0 ? "images/edits" : "images/generations");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", o.ApiKey);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", invocation.IdempotencyKey);
        var prompt = string.IsNullOrWhiteSpace(invocation.Request.NegativeInstructions) ? invocation.Request.ResolvedPrompt : $"{invocation.Request.ResolvedPrompt}\n\nConstraints: {invocation.Request.NegativeInstructions}";
        if ((invocation.Request.ReferenceImages?.Count ?? 0) > 0)
        {
            var form = new MultipartFormDataContent(); form.Add(new StringContent(invocation.Request.Model ?? o.Model), "model"); form.Add(new StringContent(prompt), "prompt"); form.Add(new StringContent(o.Size), "size"); form.Add(new StringContent(o.Quality), "quality"); form.Add(new StringContent("png"), "output_format"); form.Add(new StringContent(background), "background");
            foreach (var reference in invocation.Request.ReferenceImages!.Take(16)) { await using var input = await storage.OpenReadAsync(reference.StorageKey, ct); var referenceBytes = await ReadAllAsync(input, ct); var part = new ByteArrayContent(referenceBytes); part.Headers.ContentType = new MediaTypeHeaderValue("image/png"); form.Add(part, "image[]", Path.GetFileName(reference.StorageKey)); }
            request.Content = form;
        }
        else request.Content = JsonContent.Create(new { model = invocation.Request.Model ?? o.Model, prompt, size = o.Size, quality = o.Quality, output_format = "png", background });
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct); var body = await response.Content.ReadAsStringAsync(ct);
        var usageRequestId = response.Headers.TryGetValues("x-request-id", out var usageIds) ? usageIds.FirstOrDefault() : null;
        if (usageId is { } tracked) await usage!.ReportAsync(tracked, AiProviderUsage.Parse(body, usageRequestId), ct);
        if (!response.IsSuccessStatusCode) throw new HttpRequestException($"OpenAI image generation failed ({(int)response.StatusCode}).", null, response.StatusCode);
        using var json = JsonDocument.Parse(body); var root = json.RootElement; var first = root.GetProperty("data")[0];
        byte[] bytes; if (first.TryGetProperty("b64_json", out var b64)) bytes = Convert.FromBase64String(b64.GetString() ?? throw new InvalidDataException("OpenAI returned empty image data.")); else throw new InvalidDataException("OpenAI response did not contain PNG image data.");
        var requestId = response.Headers.TryGetValues("x-request-id", out var values) ? values.FirstOrDefault() : null;
        return new GeneratedArtMaster(bytes, requestId, null);
    }
    private static async Task<byte[]> ReadAllAsync(Stream input, CancellationToken ct) { using var ms = new MemoryStream(); await input.CopyToAsync(ms, ct); return ms.ToArray(); }
}
