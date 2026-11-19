using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Fatewake.Infrastructure.Art;
using Fatewake.Observability;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fatewake.IntegrationTests;

/// <summary>Exercises the Responses payload and structured-output boundary with a fake provider.</summary>
/// <see href="../../docs/code/tests/Fatewake.IntegrationTests/ArtPromptAuthoringTests.md">Documentation</see>
public sealed class ArtPromptAuthoringTests
{
    /// <summary>Uses the configured model and reasoning none, with strict schema and no tools.</summary>
    [Fact]
    public async Task Responses_request_uses_bounded_schema_and_records_usage()
    {
        var handler = new FakeProvider(Response("""
            {"id":"resp-test","output":[{"type":"message","content":[{"type":"output_text","text":"{\"positive_prompt\":\"A quiet interior at dawn.\",\"negative_prompt\":\"unrelated people\"}"}]}],"usage":{"input_tokens":21,"output_tokens":12}}
            """));
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/v1/") };
        var recorder = new RecordingUsage();
        var activityTags = new List<string>();
        var activityNames = new List<string>();
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == OperationTelemetry.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                activityNames.Add(activity.OperationName);
                activityTags.AddRange(activity.Tags.Select(tag => $"{tag.Key}={tag.Value}"));
            }
        };
        ActivitySource.AddActivityListener(activityListener);
        var author = new OpenAiArtPromptAuthor(client, new ArtPromptAuthoringOptions
        {
            Enabled = true,
            ApiKey = "test-key",
            Model = "gpt-6-luna",
            ReasoningEffort = "none"
        }, recorder, NullLogger<OpenAiArtPromptAuthor>.Instance);
        var context = new ArtPromptAuthoringRequest("identity", "bg-day1-bedroom-dark", "Background",
            "context", "3", "Known visual context only.", "Opaque portrait background.", "Shared style.",
            "No people.", Guid.NewGuid(), Guid.NewGuid());
        var ownerId = context.OwnerAccountId ?? throw new InvalidOperationException("The test context requires an owner.");
        var survivorId = context.SurvivorId ?? throw new InvalidOperationException("The test context requires a survivor.");

        var result = await author.AuthorAsync(context, Guid.NewGuid(), 1, CancellationToken.None);

        Assert.Equal("A quiet interior at dawn.", result.PositivePrompt);
        Assert.Equal("unrelated people", result.NegativePrompt);
        Assert.Equal(21, result.InputTokens);
        Assert.Equal(12, result.OutputTokens);
        Assert.NotNull(recorder.Report);
        Assert.Equal("succeeded", recorder.Outcome);
        Assert.Equal(context.OwnerAccountId, recorder.Context?.AccountId);
        Assert.Equal(context.SurvivorId, recorder.Context?.SurvivorId);
        Assert.Contains("art.prompt.author", activityNames);
        using var payload = JsonDocument.Parse(handler.RequestBody!);
        var root = payload.RootElement;
        Assert.Equal("gpt-6-luna", root.GetProperty("model").GetString());
        Assert.Equal("none", root.GetProperty("reasoning").GetProperty("effort").GetString());
        Assert.Empty(root.GetProperty("tools").EnumerateArray());
        Assert.Equal("json_schema", root.GetProperty("text").GetProperty("format").GetProperty("type").GetString());
        Assert.DoesNotContain(ownerId.ToString(), handler.RequestBody);
        Assert.DoesNotContain(survivorId.ToString(), handler.RequestBody);
        Assert.Contains("Known visual context only.", handler.RequestBody);
        Assert.DoesNotContain("test-key", handler.RequestBody);
        Assert.DoesNotContain(activityTags, tag =>
            tag.Contains(ownerId.ToString(), StringComparison.Ordinal) ||
            tag.Contains(survivorId.ToString(), StringComparison.Ordinal) ||
            tag.Contains("Known visual context only.", StringComparison.Ordinal) ||
            tag.Contains("A quiet interior at dawn.", StringComparison.Ordinal) ||
            tag.Contains("test-key", StringComparison.Ordinal));
        Assert.Equal("Bearer", handler.AuthorizationScheme);
    }

    /// <summary>Rejects unsafe or overlong provider content before it can become a prompt revision.</summary>
    [Theory]
    [InlineData("{\"positive_prompt\":\"art/private.png\",\"negative_prompt\":\"\"}")]
    [InlineData("{\"positive_prompt\":\"\",\"negative_prompt\":\"\"}")]
    [InlineData("{\"positive_prompt\":\"unfinished\"")]
    public async Task Invalid_structured_prompt_is_rejected(string structuredText)
    {
        var escaped = JsonSerializer.Serialize(structuredText);
        var responseBody = $$"""{"output":[{"type":"message","content":[{"type":"output_text","text":{{escaped}}}]}]}""";
        using var client = new HttpClient(new FakeProvider(Response(responseBody)))
        {
            BaseAddress = new Uri("https://example.test/v1/")
        };
        var recorder = new RecordingUsage();
        var author = new OpenAiArtPromptAuthor(client,
            new ArtPromptAuthoringOptions { Enabled = true, ApiKey = "test-key" },
            recorder, NullLogger<OpenAiArtPromptAuthor>.Instance);
        var request = new ArtPromptAuthoringRequest("identity", "prop-radio-day1", "Prop", "context",
            "3", "Known facts only.", "Isolated transparent prop.", "Style.", "No text.");

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            author.AuthorAsync(request, Guid.NewGuid(), 1, CancellationToken.None));
        Assert.Equal("failed", recorder.Outcome);
    }

    /// <summary>Marks provider errors while keeping response bodies and request context out of telemetry.</summary>
    [Fact]
    public async Task Provider_failure_telemetry_omits_response_body_and_request_context()
    {
        const string privateProviderBody = "sensitive-provider-response";
        var handler = new FakeProvider(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
            Content = new StringContent(privateProviderBody, Encoding.UTF8, "application/json")
        });
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/v1/") };
        var recorder = new RecordingUsage();
        var activityTags = new List<string>();
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == OperationTelemetry.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => activityTags.AddRange(activity.Tags.Select(tag =>
                $"{tag.Key}={tag.Value}"))
        };
        ActivitySource.AddActivityListener(activityListener);
        var author = new OpenAiArtPromptAuthor(client,
            new ArtPromptAuthoringOptions { Enabled = true, ApiKey = "test-key" },
            recorder, NullLogger<OpenAiArtPromptAuthor>.Instance);
        var request = new ArtPromptAuthoringRequest("private-identity", "prop-radio-day1", "Prop",
            "private-context-fingerprint", "3", "Private facts captured for this request.",
            "Transparent single prop.", "Style.", "No text.");

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            author.AuthorAsync(request, Guid.NewGuid(), 1, CancellationToken.None));

        Assert.Equal("failed", recorder.Outcome);
        Assert.Contains(activityTags, tag => tag.Contains("operation.outcome=error", StringComparison.Ordinal));
        Assert.Contains(activityTags, tag => tag.Contains("System.Net.Http.HttpRequestException", StringComparison.Ordinal));
        Assert.DoesNotContain(activityTags, tag =>
            tag.Contains(privateProviderBody, StringComparison.Ordinal) ||
            tag.Contains("Private facts captured for this request.", StringComparison.Ordinal) ||
            tag.Contains("private-context-fingerprint", StringComparison.Ordinal) ||
            tag.Contains("test-key", StringComparison.Ordinal));
    }

    private static HttpResponseMessage Response(string body) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

    private sealed class FakeProvider(HttpResponseMessage response) : HttpMessageHandler
    {
        public string? RequestBody
        {
            get; private set;
        }
        public string? AuthorizationScheme
        {
            get; private set;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            RequestBody = await request.Content!.ReadAsStringAsync(ct);
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            return response;
        }
    }

    private sealed class RecordingUsage : IAiUsageRecorder
    {
        public AiCallContext? Context
        {
            get; private set;
        }

        public AiUsageReport? Report
        {
            get; private set;
        }
        public string? Outcome
        {
            get; private set;
        }
        public Task<Guid> BeginAsync(AiCallContext context, CancellationToken ct = default)
        {
            Context = context;
            return Task.FromResult(Guid.NewGuid());
        }
        public Task ReportAsync(Guid attemptId, AiUsageReport report, CancellationToken ct = default)
        {
            Report = report;
            return Task.CompletedTask;
        }
        public Task FinishAsync(Guid attemptId, string outcome, CancellationToken ct = default)
        {
            Outcome = outcome;
            return Task.CompletedTask;
        }
    }
}
