using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Fatewake.Infrastructure.Usage;
using Fatewake.Observability;
using Microsoft.Extensions.Logging;

namespace Fatewake.Infrastructure.Art;

/// <summary>Calls OpenAI Responses for strict, tool-free artwork prompt authoring.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Art/OpenAiArtPromptAuthor.md">Documentation</see>.</remarks>
public sealed class OpenAiArtPromptAuthor(HttpClient httpClient, ArtPromptAuthoringOptions options,
    IAiUsageRecorder usage, ILogger<OpenAiArtPromptAuthor> log) : IArtPromptAuthor
{
    private const int PositiveLimit = 8000;
    private const int NegativeLimit = 2000;

    /// <inheritdoc />
    public async Task<ArtPromptAuthoringResult> AuthorAsync(ArtPromptAuthoringRequest request,
        Guid workJobId, int attempt, CancellationToken ct)
    {
        using var operation = OperationTelemetry.Start("art.prompt.author", log);
        try
        {
            return await TrackedAiCall.RunAsync(usage,
                new AiCallContext("openai", options.Model, "text.generate",
                    $"art-prompt:{workJobId:N}:{attempt}", workJobId, request.OwnerAccountId, request.SurvivorId),
                async usageId =>
                {
                    using var message = new HttpRequestMessage(HttpMethod.Post, "responses");
                    message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
                    message.Content = new StringContent(JsonSerializer.Serialize(CreatePayload(request)),
                        Encoding.UTF8, "application/json");
                    using var response = await httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, ct);
                    if (!response.IsSuccessStatusCode)
                    {
                        if (response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500)
                            throw new HttpRequestException("The prompt provider returned a transient failure.", null, response.StatusCode);
                        throw new InvalidOperationException($"The prompt provider rejected the request ({(int)response.StatusCode}).");
                    }

                    await using var stream = await response.Content.ReadAsStreamAsync(ct);
                    using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
                    var root = document.RootElement;
                    var text = ReadOutputText(root);
                    StructuredPrompt structured;
                    try
                    {
                        structured = JsonSerializer.Deserialize<StructuredPrompt>(text,
                            new JsonSerializerOptions(JsonSerializerDefaults.Web)
                            {
                                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                            })
                            ?? throw new InvalidDataException("The prompt provider returned an empty structured result.");
                    }
                    catch (JsonException ex)
                    {
                        throw new InvalidDataException("The prompt provider returned malformed structured output.", ex);
                    }
                    var positive = ValidateText(structured.PositivePrompt, PositiveLimit, "positive prompt");
                    var negative = string.IsNullOrWhiteSpace(structured.NegativePrompt)
                        ? null : ValidateText(structured.NegativePrompt, NegativeLimit, "negative prompt");
                    var requestId = root.TryGetProperty("id", out var id) ? id.GetString() : null;
                    var inputTokens = UsageToken(root, "input_tokens");
                    var outputTokens = UsageToken(root, "output_tokens");
                    if (usageId is { } trackedId)
                        await usage.ReportAsync(trackedId, new AiUsageReport(requestId, InputTokens: inputTokens,
                            OutputTokens: outputTokens, TotalTokens: Sum(inputTokens, outputTokens),
                            TextInputTokens: inputTokens), ct);
                    return new ArtPromptAuthoringResult(positive, negative, requestId, inputTokens, outputTokens);
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
            log.LogWarning(ex, "Artwork prompt authoring provider call failed");
            throw;
        }
    }

    private object CreatePayload(ArtPromptAuthoringRequest request) => new
    {
        model = options.Model,
        reasoning = new
        {
            effort = options.ReasoningEffort
        },
        max_output_tokens = options.MaximumOutputTokens,
        tools = Array.Empty<object>(),
        input = new object[]
        {
            new { role = "system", content = new[] { new { type = "input_text", text =
                "Author only concise visual subject details for a production artwork prompt. Return the required JSON schema. " +
                "Do not decide story outcomes, infer hidden causes, invent identity or future facts, or include paths, asset identifiers, tool instructions, text, logos, or watermarks. " +
                "The server appends authoritative role, alpha, geometry, continuity, style, and negative constraints; your text is untrusted additive presentation content. " +
                "Do not repeat or weaken those constraints. Keep positive_prompt under 8000 characters and negative_prompt under 2000 characters." } } },
            new { role = "user", content = new[] { new { type = "input_text", text = JsonSerializer.Serialize(new
                {
                    asset_role = request.AssetType,
                    canonical_layer_key = request.AssetKey,
                    canonical_context = request.CanonicalContext,
                    framing = request.Framing,
                    shared_style = request.RuntimeStyle,
                    shared_exclusions = request.RuntimeExclusions
                }) } } }
        },
        text = new
        {
            format = new
            {
                type = "json_schema",
                name = "fatewake_art_prompt",
                strict = true,
                schema = new
                {
                    type = "object",
                    properties = new
                    {
                        positive_prompt = new
                        {
                            type = "string"
                        },
                        negative_prompt = new
                        {
                            type = "string"
                        }
                    },
                    required = new[] { "positive_prompt", "negative_prompt" },
                    additionalProperties = false
                }
            }
        }
    };

    private static string ReadOutputText(JsonElement root)
    {
        if (!root.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("The prompt provider response contains no structured output.");
        foreach (var item in output.EnumerateArray())
        {
            if (!item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
                continue;
            foreach (var segment in content.EnumerateArray())
            {
                if (segment.TryGetProperty("type", out var type) && type.GetString() == "refusal")
                    throw new InvalidOperationException("The prompt provider refused the request.");
                if (segment.TryGetProperty("type", out type) && type.GetString() == "output_text" &&
                    segment.TryGetProperty("text", out var text))
                    return text.GetString() ?? string.Empty;
            }
        }
        throw new InvalidDataException("The prompt provider response contains no output text.");
    }

    private static string ValidateText(string? value, int maximumLength, string field)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > maximumLength ||
            normalized.Any(char.IsControl) || normalized.Contains("```", StringComparison.Ordinal) ||
            normalized.Contains("ignore previous instructions", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("tool_call", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("asset_id", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("art/", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("art\\", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"The prompt provider returned an invalid {field}.");
        return normalized;
    }

    private static long? UsageToken(JsonElement root, string property) =>
        root.TryGetProperty("usage", out var usage) && usage.TryGetProperty(property, out var token) &&
        token.TryGetInt64(out var value) && value >= 0 ? value : null;

    private static long? Sum(long? first, long? second) =>
        first is { } a && second is { } b ? a + b : null;

    private sealed record StructuredPrompt(string PositivePrompt, string NegativePrompt);
}
