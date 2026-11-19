using Fatewake.Infrastructure.Usage;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Fatewake.Observability;
using Microsoft.Extensions.Logging;
namespace Fatewake.Infrastructure.Art;
/// <summary>Generates master PNG artwork through ComfyUI, uploading approved continuity references before workflow submission.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Art/ComfyUiArtMasterGenerator.md">ComfyUiArtMasterGenerator documentation</see>.</remarks>
public sealed class ComfyUiArtMasterGenerator(HttpClient http, IOptions<ComfyUiArtGenerationOptions> configured, IArtBinaryStorage storage, ILogger<ComfyUiArtMasterGenerator>? log = null, IAiUsageRecorder? usage = null) : IArtMasterGenerator
{
    public Task<GeneratedArtMaster> GenerateAsync(ArtGenerationInvocation invocation, CancellationToken ct = default)
       => TrackedAiCall.RunAsync(usage, new AiCallContext(invocation.Request.Provider, invocation.Request.Model,
           (invocation.Request.ReferenceImages?.Count ?? 0) > 0 ? "image.edit" : "image.generate",
           $"{invocation.IdempotencyKey}:attempt:{invocation.Attempt}", invocation.WorkJobId, invocation.Request.AccountId, invocation.Request.SurvivorId),
           id => GenerateCoreAsync(invocation, id, ct), ct);
    private async Task<GeneratedArtMaster> GenerateCoreAsync(ArtGenerationInvocation invocation, Guid? usageId, CancellationToken ct)
    {
        using var operation = OperationTelemetry.Start("art.generate.comfyui", log);
        var o = configured.Value; if (!o.Enabled) throw new InvalidOperationException("ComfyUI art generation is not enabled.");
        var overlay = invocation.Request.AssetType is "CharacterPose" or "CharacterExpression" or "Prop" or "Foreground" or "Lighting" or "Effect" or "Atmosphere";
        var path = Path.GetFullPath(overlay && !string.IsNullOrWhiteSpace(o.OverlayWorkflowPath) ? o.OverlayWorkflowPath : o.WorkflowPath); if (!File.Exists(path)) throw new FileNotFoundException("ComfyUI API workflow was not found.", path);
        var template = await File.ReadAllTextAsync(path, ct);
        var workflow = template.Replace(o.PromptToken, JsonEncodedText.Encode(invocation.Request.ResolvedPrompt).ToString(), StringComparison.Ordinal)
         .Replace(o.NegativePromptToken, JsonEncodedText.Encode(invocation.Request.NegativeInstructions ?? string.Empty).ToString(), StringComparison.Ordinal)
         .Replace(o.SeedToken, StableSeed(invocation.IdempotencyKey).ToString(), StringComparison.Ordinal);
        if (workflow.Contains(ComfyUiArtGenerationOptions.ModelToken, StringComparison.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(invocation.Request.Model))
                throw new InvalidOperationException("A ComfyUI model is required when the workflow contains the model token.");
            workflow = workflow.Replace(ComfyUiArtGenerationOptions.ModelToken, JsonEncodedText.Encode(invocation.Request.Model).ToString(), StringComparison.Ordinal);
        }
        if (invocation.Request.ReferenceImages is not null)
            foreach (var reference in invocation.Request.ReferenceImages)
            {
                var uploaded = await UploadReferenceAsync(o, reference, invocation.IdempotencyKey, ct);
                var token = $"{o.ReferenceTokenPrefix}{NormalizeRole(reference.Role)}__";
                workflow = workflow.Replace(token, JsonEncodedText.Encode(uploaded).ToString(), StringComparison.Ordinal);
            }
        using var doc = JsonDocument.Parse(workflow);
        using var submit = await SendAsync(o, HttpMethod.Post, "prompt", JsonContent.Create(new { prompt = doc.RootElement.Clone(), client_id = invocation.IdempotencyKey }), ct);
        var submitBody = await submit.Content.ReadAsStringAsync(ct); if (!submit.IsSuccessStatusCode) throw new HttpRequestException($"ComfyUI submission failed ({(int)submit.StatusCode}).", null, submit.StatusCode);
        using var submitted = JsonDocument.Parse(submitBody); var promptId = submitted.RootElement.GetProperty("prompt_id").GetString() ?? throw new InvalidDataException("ComfyUI returned no prompt_id.");
        if (usageId is { } submittedAttempt) await usage!.ReportAsync(submittedAttempt, AiProviderUsage.Parse(submitted.RootElement, promptId), ct);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(o.TimeoutSeconds));
        while (true)
        {
            timeout.Token.ThrowIfCancellationRequested(); using var history = await SendAsync(o, HttpMethod.Get, $"{o.HistoryPath.Trim('/')}/{Uri.EscapeDataString(promptId)}", null, timeout.Token);
            var body = await history.Content.ReadAsStringAsync(timeout.Token); history.EnsureSuccessStatusCode(); using var json = JsonDocument.Parse(body);
            if (usageId is { } historyAttempt && json.RootElement.TryGetProperty(promptId, out var usageEntry) && usageEntry.TryGetProperty("usage", out _))
                await usage!.ReportAsync(historyAttempt, AiProviderUsage.Parse(usageEntry, promptId), timeout.Token);
            if (json.RootElement.TryGetProperty(promptId, out var failed) && failed.TryGetProperty("status", out var status) &&
               status.TryGetProperty("status_str", out var state) && state.GetString() == "error")
                throw new InvalidOperationException($"ComfyUI workflow {promptId} failed. Check the ComfyUI server log.");
            if (json.RootElement.TryGetProperty(promptId, out var entry) && entry.TryGetProperty("outputs", out var outputs))
                foreach (var node in outputs.EnumerateObject()) if (node.Value.TryGetProperty("images", out var images) && images.GetArrayLength() > 0)
                {
                    var image = images[0]; var filename = image.GetProperty("filename").GetString()!; var subfolder = image.TryGetProperty("subfolder", out var sf) ? sf.GetString() ?? "" : ""; var type = image.TryGetProperty("type", out var ty) ? ty.GetString() ?? "output" : "output";
                    var url = $"view?filename={Uri.EscapeDataString(filename)}&subfolder={Uri.EscapeDataString(subfolder)}&type={Uri.EscapeDataString(type)}";
                    return new GeneratedArtMaster(await DownloadAsync(o, url, timeout.Token), promptId, null);
                }
            await Task.Delay(o.PollIntervalMilliseconds, timeout.Token);
        }
    }
    private async Task<string> UploadReferenceAsync(ComfyUiArtGenerationOptions o, ArtReferenceImage reference, string jobKey, CancellationToken ct)
    {
        await using var source = await storage.OpenReadAsync(reference.StorageKey, ct); using var ms = new MemoryStream(); await source.CopyToAsync(ms, ct);
        using var form = new MultipartFormDataContent(); var part = new ByteArrayContent(ms.ToArray()); part.Headers.ContentType = new("image/png");
        var name = $"fatewake/{Sanitize(jobKey)}/{NormalizeRole(reference.Role)}-{Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(ms.ToArray())).ToLowerInvariant()[..16]}.png";
        form.Add(part, "image", Path.GetFileName(name)); form.Add(new StringContent(Path.GetDirectoryName(name)!.Replace('\\', '/')), "subfolder"); form.Add(new StringContent("true"), "overwrite");
        using var response = await SendAsync(o, HttpMethod.Post, "upload/image", form, ct); var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode) throw new HttpRequestException($"ComfyUI reference upload failed ({(int)response.StatusCode}).", null, response.StatusCode);
        using var json = JsonDocument.Parse(body); return json.RootElement.TryGetProperty("subfolder", out var sf) && !string.IsNullOrWhiteSpace(sf.GetString()) ? $"{sf.GetString()}/{json.RootElement.GetProperty("name").GetString()}" : json.RootElement.GetProperty("name").GetString()!;
    }
    private async Task<HttpResponseMessage> SendAsync(ComfyUiArtGenerationOptions o, HttpMethod method, string url, HttpContent? content, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, url) { Content = content };
        if (o.ApiKey is not null)
        {
            if (string.Equals(o.ApiKeyHeader, "Authorization", StringComparison.OrdinalIgnoreCase)) request.Headers.Authorization = new("Bearer", o.ApiKey);
            else request.Headers.TryAddWithoutValidation(o.ApiKeyHeader, o.ApiKey);
        }
        return await http.SendAsync(request, ct);
    }
    // The API key is attached only to ComfyUI requests; redirect targets (for example signed storage URLs) are fetched without it.
    private async Task<byte[]> DownloadAsync(ComfyUiArtGenerationOptions o, string url, CancellationToken ct)
    {
        using var response = await SendAsync(o, HttpMethod.Get, url, null, ct);
        if ((int)response.StatusCode is >= 300 and < 400 && response.Headers.Location is { } location)
        {
            var target = location.IsAbsoluteUri ? location : new Uri(response.RequestMessage?.RequestUri ?? http.BaseAddress!, location);
            using var redirected = await http.GetAsync(target, ct); redirected.EnsureSuccessStatusCode();
            return await redirected.Content.ReadAsByteArrayAsync(ct);
        }
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(ct);
    }
    private static string NormalizeRole(string role) => new string(role.Trim().ToUpperInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
    private static string Sanitize(string value) => new string(value.Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_').ToArray());
    private static long StableSeed(string key) { var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key)); return BitConverter.ToInt64(hash, 0) & long.MaxValue; }
}
