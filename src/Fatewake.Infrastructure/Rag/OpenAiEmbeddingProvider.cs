using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Fatewake.Infrastructure.Usage;
using Fatewake.Observability;
using Microsoft.Extensions.Logging;

namespace Fatewake.Infrastructure.Rag;

/// <summary>Calls the OpenAI embeddings endpoint with usage tracking and transient-failure signalling.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Rag/OpenAiEmbeddingProvider.md">Documentation</see>.</remarks>
public sealed class OpenAiEmbeddingProvider(HttpClient httpClient, EmbeddingOptions options,
    IAiUsageRecorder usage, ILogger<OpenAiEmbeddingProvider> log) : IEmbeddingProvider
{
    /// <inheritdoc />
    public string Model => options.Model;

    /// <inheritdoc />
    public int Dimensions => options.Dimensions;

    /// <inheritdoc />
    public async Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> inputs, Guid? accountId,
        Guid? survivorId, CancellationToken ct)
    {
        if (inputs.Count == 0)
            return [];
        using var operation = OperationTelemetry.Start("rag.embed", log);
        try
        {
            return await TrackedAiCall.RunAsync(usage,
                new AiCallContext("openai", options.Model, "text.embed", $"rag-embed:{Guid.NewGuid():N}",
                    null, accountId, survivorId),
                async usageId =>
                {
                    using var message = new HttpRequestMessage(HttpMethod.Post, "embeddings");
                    message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
                    message.Content = new StringContent(JsonSerializer.Serialize(new
                    {
                        model = options.Model,
                        input = inputs,
                        dimensions = options.Dimensions,
                        encoding_format = "float"
                    }), Encoding.UTF8, "application/json");
                    using var response = await httpClient.SendAsync(message, ct);
                    if (!response.IsSuccessStatusCode)
                    {
                        if (response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500)
                            throw new HttpRequestException("The embedding provider returned a transient failure.", null, response.StatusCode);
                        throw new InvalidOperationException($"The embedding provider rejected the request ({(int)response.StatusCode}).");
                    }

                    await using var stream = await response.Content.ReadAsStreamAsync(ct);
                    using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
                    var root = document.RootElement;
                    if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array ||
                        data.GetArrayLength() != inputs.Count)
                        throw new InvalidDataException("The embedding provider returned an unexpected result count.");
                    var vectors = new float[inputs.Count][];
                    foreach (var item in data.EnumerateArray())
                    {
                        var index = item.GetProperty("index").GetInt32();
                        var vector = item.GetProperty("embedding").EnumerateArray().Select(x => x.GetSingle()).ToArray();
                        if (index < 0 || index >= vectors.Length || vector.Length != options.Dimensions)
                            throw new InvalidDataException("The embedding provider returned a malformed vector.");
                        vectors[index] = vector;
                    }
                    if (vectors.Any(x => x is null))
                        throw new InvalidDataException("The embedding provider omitted a vector.");

                    var tokens = root.TryGetProperty("usage", out var u) && u.TryGetProperty("prompt_tokens", out var t) &&
                        t.TryGetInt64(out var value) ? value : (long?)null;
                    if (usageId is { } trackedId)
                        await usage.ReportAsync(trackedId, new AiUsageReport(InputTokens: tokens, TotalTokens: tokens,
                            TextInputTokens: tokens), ct);
                    return (IReadOnlyList<float[]>)vectors;
                }, ct);
        }
        catch (OperationCanceledException ex) when (ct.IsCancellationRequested)
        {
            operation.Fail(ex);
            throw;
        }
        catch (Exception ex)
        {
            operation.Fail(ex);
            log.LogWarning(ex, "Embedding provider call failed");
            throw;
        }
    }
}
