using System.Text.Json;
using Fatewake.Observability;
namespace Fatewake.Infrastructure.Usage;
/// <summary>Reads an allowlist of reported measurements, excluding raw responses and secrets.</summary>
public static class AiProviderUsage
{
    /// <summary>Extracts standard compatible token counters and explicitly supplied costs/credits.</summary>
    public static AiUsageReport Parse(string body, string? requestId = null)
    {
        try { using var doc = JsonDocument.Parse(body); return Parse(doc.RootElement, requestId); }
        catch (JsonException) { return new(requestId); }
    }
    /// <summary>Extracts measurements from one provider response or ComfyUI history entry.</summary>
    public static AiUsageReport Parse(JsonElement root, string? requestId = null)
    {
        var usage = Child(root, "usage"); var details = Child(usage, "input_tokens_details");
        if (details.ValueKind != JsonValueKind.Object) details = Child(usage, "prompt_tokens_details");
        var output = Child(usage, "output_tokens_details");
        return new(requestId, Long(usage, "input_tokens") ?? Long(usage, "prompt_tokens"), Long(usage, "output_tokens") ?? Long(usage, "completion_tokens"),
            Long(usage, "total_tokens"), Long(details, "cached_tokens"), Long(details, "text_tokens"), Long(details, "image_tokens"), Long(output, "image_tokens"),
            Decimal(usage, "credits"), Decimal(usage, "compute_seconds"), Decimal(usage, "cost_usd") ?? Decimal(usage, "cost"),
            Decimal(usage, "cost_usd") is not null ? "USD" : Text(usage, "currency"));
    }
    private static JsonElement Child(JsonElement x, string key) => x.ValueKind == JsonValueKind.Object && x.TryGetProperty(key, out var value) ? value : default;
    private static long? Long(JsonElement x, string key) => Child(x, key) is var value && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number) && number >= 0 ? number : null;
    private static decimal? Decimal(JsonElement x, string key) => Child(x, key) is var value && value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number) && number >= 0 ? number : null;
    private static string? Text(JsonElement x, string key) => Child(x, key) is var value && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
