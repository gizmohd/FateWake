using System.Net;
using System.Text.Json;

namespace Fatewake.Web.Presentation;

/// <summary>Safe, bounded diagnostics for an administrator's failed RAG request; never holds a raw response or exception.</summary>
/// <see href="../../../docs/code/src/Fatewake.Web/Presentation/RagRequestError.md">Documentation</see>
public sealed record RagRequestError(string Operation, string Message, string Detail, int? StatusCode,
    string Code, string? ReferenceId, DateTimeOffset OccurredAt)
{
    /// <summary>Stops repeat polling or uploads when a new sign-in or administrator permission is needed.</summary>
    public bool RequiresAuthentication => StatusCode is 401 or 403;

    /// <summary>Reads only bounded JSON error fields; HTML, stack traces and unexpected response bodies are never displayed.</summary>
    public static async Task<RagRequestError> ReadAsync(HttpResponseMessage response, string operation, CancellationToken ct = default)
    {
        var (message, detail, code) = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => ("Your API sign-in is no longer valid. Sign in again.",
                "The API rejected authentication. Your session may have expired or been revoked; no import was accepted.", "rag.sign_in_required"),
            HttpStatusCode.Forbidden => ("Administrator access is required.",
                "The API denied permission to manage the RAG corpus. Use an active administrator account.", "rag.forbidden"),
            HttpStatusCode.RequestEntityTooLarge => ("The document upload is too large.",
                "The API or its proxy rejected the request. The document limit is 100 MiB; a proxy may impose a smaller limit.", "rag.upload_too_large"),
            HttpStatusCode.BadRequest => ("The document upload is invalid.",
                "Check the selected file and required source key and title.", "rag.invalid_upload"),
            _ => ("The RAG request failed.",
                "Retry later. Use the reference ID to locate server logs. No readable error details were returned.", "rag.request_failed")
        };
        var reference = response.Headers.TryGetValues("X-Request-ID", out var references) ? references.FirstOrDefault() : null;
        var result = new RagRequestError(operation, message, detail, (int)response.StatusCode, code, reference, DateTimeOffset.UtcNow);
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (mediaType is not ("application/problem+json" or "application/json"))
            return result;
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var bytes = new byte[16 * 1024 + 1];
        var length = await stream.ReadAtLeastAsync(bytes, bytes.Length, throwOnEndOfStream: false, ct);
        if (length == bytes.Length)
            return result with
            {
                Detail = detail + " The diagnostic response exceeded the safe display limit."
            };
        if (length == 0)
            return result;
        try
        {
            using var json = JsonDocument.Parse(bytes.AsMemory(0, length));
            if (json.RootElement.ValueKind != JsonValueKind.Object)
                return result with
                {
                    Detail = detail + " The API returned an unexpected diagnostic format."
                };
            static string? Field(JsonElement root, string name, int maximum) =>
                root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String &&
                value.GetString() is { } text && text.Length <= maximum ? text : null;
            return result with
            {
                Detail = Field(json.RootElement, "detail", 4000) ?? Field(json.RootElement, "error", 4000) ?? detail,
                Code = Field(json.RootElement, "code", 100) ?? code,
                ReferenceId = Field(json.RootElement, "traceId", 200) ?? reference
            };
        }
        catch (JsonException)
        {
            return result with
            {
                Detail = detail + " The API returned malformed JSON diagnostics."
            };
        }
    }
}
