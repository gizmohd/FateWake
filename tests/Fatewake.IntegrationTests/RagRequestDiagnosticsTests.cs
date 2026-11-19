using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Fatewake.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Fatewake.IntegrationTests;

/// <summary>Exercises RAG diagnostic responses at the HTTP handling boundary without database or document contents.</summary>
/// <see href="../../docs/code/tests/Fatewake.IntegrationTests/RagRequestDiagnosticsTests.md">Documentation</see>
public sealed class RagRequestDiagnosticsTests
{
    /// <summary>Preserves rejection status and bearer challenges and correlates empty failure bodies.</summary>
    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(413)]
    public async Task Empty_rejections_return_correlated_problem_details(int status)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        await using var app = builder.Build();
        app.UseMiddleware<RagRequestDiagnostics>();
        app.Run(context =>
        {
            context.Response.StatusCode = status;
            context.Response.Headers.WWWAuthenticate = "Bearer";
            return Task.CompletedTask;
        });
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();
        using var response = await client.PostAsync("/api/admin/rag/import", null, TestContext.Current.CancellationToken);
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("Bearer", response.Headers.WwwAuthenticate.Single().Scheme);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(status, problem.GetProperty("status").GetInt32());
        Assert.Equal(response.Headers.GetValues("X-Request-ID").Single(), problem.GetProperty("traceId").GetString());
    }

    /// <summary>Returns safe server diagnostics and records an error span without disclosing exception payloads.</summary>
    [Fact]
    public async Task Unexpected_server_failure_is_safe_and_observable()
    {
        Activity? completed = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == Fatewake.Observability.OperationTelemetry.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                if (activity.OperationName == "rag.request") completed = activity;
            }
        };
        ActivitySource.AddActivityListener(listener);
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        await using var app = builder.Build();
        app.UseMiddleware<RagRequestDiagnostics>();
        app.Run(_ => throw new InvalidOperationException("private-document-and-credential-marker"));
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();
        using var response = await client.PostAsync("/api/admin/rag/import", null, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("private-document-and-credential-marker", body);
        Assert.Contains("rag.service_error", body);
        Assert.NotNull(completed);
        Assert.Equal(ActivityStatusCode.Error, completed.Status);
    }
}
