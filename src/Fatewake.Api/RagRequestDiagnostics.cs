using System.Diagnostics;
using Fatewake.Observability;

namespace Fatewake.Api;

/// <summary>Returns safe, correlated diagnostics for RAG requests, including authorization failures with empty bodies.</summary>
/// <see href="../../docs/code/src/Fatewake.Api/RagRequestDiagnostics.md">Documentation</see>
public sealed class RagRequestDiagnostics(RequestDelegate next, ILogger<RagRequestDiagnostics> logger)
{
    /// <summary>Preserves HTTP status and authentication challenges without returning exceptions or uploaded content.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api/admin/rag"))
        {
            await next(context);
            return;
        }
        using var operation = OperationTelemetry.Start("rag.request", logger);
        var reference = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
        context.Response.Headers["X-Request-ID"] = reference;
        try
        {
            await next(context);
            if (context.Response.StatusCode >= 400 && !context.Response.HasStarted &&
                context.Response.ContentLength is null or 0)
                await WriteProblemAsync(context, context.Response.StatusCode, reference);
            if (HttpMethods.IsPost(context.Request.Method) && context.Response.StatusCode < 400)
                logger.LogInformation("RAG staging request completed with HTTP {StatusCode}; reference {ReferenceId}",
                    context.Response.StatusCode, reference);
        }
        catch (OperationCanceledException ex) when (context.RequestAborted.IsCancellationRequested)
        {
            operation.Fail(ex);
            throw;
        }
        catch (BadHttpRequestException ex) when (!context.Response.HasStarted)
        {
            logger.LogWarning("RAG upload rejected with HTTP {StatusCode}; reference {ReferenceId}", ex.StatusCode, reference);
            await WriteProblemAsync(context, ex.StatusCode, reference);
        }
        catch (InvalidDataException) when (!context.Response.HasStarted)
        {
            logger.LogWarning("RAG multipart upload was invalid; reference {ReferenceId}", reference);
            await WriteProblemAsync(context, StatusCodes.Status400BadRequest, reference);
        }
        catch (Exception ex) when (!context.Response.HasStarted)
        {
            operation.Fail(ex);
            logger.LogError("RAG request failed with {ExceptionType}; reference {ReferenceId}", ex.GetType().FullName, reference);
            await WriteProblemAsync(context, StatusCodes.Status500InternalServerError, reference);
        }
    }

    private static Task WriteProblemAsync(HttpContext context, int status, string reference)
    {
        var (title, detail, code) = status switch
        {
            400 => ("Invalid document upload", "The multipart upload could not be read. Reselect the file, check its metadata, and retry. The document limit is 100 MiB.", "rag.invalid_upload"),
            401 => ("API sign-in required", "The API rejected your sign-in. Your session may have expired or been revoked. Sign in again before importing the document.", "rag.sign_in_required"),
            403 => ("Administrator access required", "The API did not authorize this account to manage the RAG corpus. Sign in with an active administrator account.", "rag.forbidden"),
            413 => ("Document upload too large", "The request exceeded the upload limit. Choose a document no larger than 100 MiB.", "rag.upload_too_large"),
            429 => ("Too many requests", "Wait before retrying the import.", "rag.rate_limited"),
            _ => ("RAG service error", "The server could not complete the RAG request. Retry later and use the reference ID to locate the server logs.", "rag.service_error")
        };
        context.Response.Headers["X-Request-ID"] = reference;
        return Results.Problem(statusCode: status, title: title, detail: detail,
            extensions: new Dictionary<string, object?> { ["code"] = code, ["traceId"] = reference }).ExecuteAsync(context);
    }
}
