using System.Net;
using System.Text;
using Fatewake.Infrastructure.Rag;
using Fatewake.Observability;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fatewake.IntegrationTests;

/// <summary>Verifies embedding request shape, response validation, and pgvector literal formatting.</summary>
public sealed class OpenAiEmbeddingProviderTests
{
    /// <summary>Orders vectors by returned index, requests the configured dimensions, and reports usage.</summary>
    [Fact]
    public async Task Embeds_inputs_in_order_and_reports_usage()
    {
        var handler = new StubHandler("""
            {"data":[{"index":1,"embedding":[0.5,0.25]},{"index":0,"embedding":[1,2]}],"usage":{"prompt_tokens":7}}
            """);
        var usage = new RecordingUsage();
        var provider = Create(handler, usage, 2);

        var vectors = await provider.EmbedAsync(["a", "b"], null, null, TestContext.Current.CancellationToken);

        Assert.Equal([1f, 2f], vectors[0]);
        Assert.Equal([0.5f, 0.25f], vectors[1]);
        Assert.Contains("\"dimensions\":2", handler.RequestBody);
        Assert.Equal(7, usage.InputTokens);
    }

    /// <summary>Rejects vectors whose length does not match the configured column size.</summary>
    [Fact]
    public async Task Rejects_wrong_dimensions()
    {
        var provider = Create(new StubHandler("""{"data":[{"index":0,"embedding":[1]}]}"""), new RecordingUsage(), 2);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            provider.EmbedAsync(["a"], null, null, TestContext.Current.CancellationToken));
    }

    /// <summary>Formats pgvector literals with invariant culture.</summary>
    [Fact]
    public void Formats_vector_literal() =>
        Assert.Equal("[0.5,-1,2.25]", RagEmbeddingService.FormatVector([0.5f, -1f, 2.25f]));

    private static OpenAiEmbeddingProvider Create(StubHandler handler, RecordingUsage usage, int dimensions) =>
        new(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://example.test/v1/")
        },
            new EmbeddingOptions
            {
                Enabled = true,
                ApiKey = "k",
                Dimensions = dimensions
            }, usage, NullLogger<OpenAiEmbeddingProvider>.Instance);

    private sealed class StubHandler(string body) : HttpMessageHandler
    {
        public string? RequestBody
        {
            get; private set;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            RequestBody = await request.Content!.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class RecordingUsage : IAiUsageRecorder
    {
        public long? InputTokens
        {
            get; private set;
        }
        public Task<Guid> BeginAsync(AiCallContext context, CancellationToken ct = default) => Task.FromResult(Guid.NewGuid());
        public Task ReportAsync(Guid attemptId, AiUsageReport report, CancellationToken ct = default)
        {
            InputTokens = report.InputTokens;
            return Task.CompletedTask;
        }
        public Task FinishAsync(Guid attemptId, string outcome, CancellationToken ct = default) => Task.CompletedTask;
    }
}
