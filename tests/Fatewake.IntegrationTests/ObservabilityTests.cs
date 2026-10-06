using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Fatewake.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Fatewake.IntegrationTests;

/// <summary>Verifies operation telemetry and actual OTLP log/trace/metric delivery from Microsoft typed logging.</summary>
/// <see href="../../docs/code/tests/Fatewake.IntegrationTests/ObservabilityTests.md">Documentation</see>
public sealed class ObservabilityTests
{
    /// <summary>Invalid sampling configuration fails startup instead of silently ignoring the requested policy.</summary>
    [Fact]
    public void Invalid_sampling_ratio_is_rejected()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Telemetry:TraceSampleRatio"] = "2" });
        Assert.Throws<InvalidOperationException>(() => builder.AddObservability());
    }

    /// <summary>Scopes preserve parenting, report failures without exception payloads, and emit bounded-cardinality duration exactly once.</summary>
    [Fact]
    public void Operation_scopes_record_parenting_failure_and_latency()
    {
        var spans = new List<Activity>();
        var durations = new List<double>();
        using var activities = new ActivityListener
        {
            ShouldListenTo = source => source.Name == OperationTelemetry.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => { if (activity.OperationName == "test.operation") spans.Add(activity); }
        };
        ActivitySource.AddActivityListener(activities);
        using var metrics = new MeterListener();
        metrics.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == OperationTelemetry.SourceName) listener.EnableMeasurementEvents(instrument);
        };
        metrics.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
        {
            if (instrument.Name != "fatewake.operation.duration") return;
            if (!tags.ToArray().Any(tag => tag.Key == "operation" && Equals(tag.Value, "test.operation"))) return;
            Assert.All(tags.ToArray(), tag => Assert.Contains(tag.Key, new[] { "operation", "outcome" }));
            durations.Add(value);
        });
        metrics.Start();
        using var parent = new Activity("parent").Start();
        var operation = OperationTelemetry.Start("test.operation");
        operation.Fail(new InvalidOperationException("sensitive-payload"));
        operation.Dispose();
        operation.Dispose();
        var span = Assert.Single(spans);
        Assert.Equal(parent.TraceId, span.TraceId);
        Assert.Equal(parent.SpanId, span.ParentSpanId);
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.DoesNotContain(span.TagObjects, tag => tag.Value?.ToString()?.Contains("sensitive-payload") == true);
        Assert.True(Assert.Single(durations) >= 0);
    }

    /// <summary>All three signals reach a local authenticated OTLP collector using HTTP/protobuf and contain the canonical service identity.</summary>
    [Fact]
    public async Task Typed_logging_traces_and_metrics_reach_otlp_collector()
    {
        var ct = TestContext.Current.CancellationToken;
        using var portProbe = new TcpListener(IPAddress.Loopback, 0);
        portProbe.Start();
        var port = ((IPEndPoint)portProbe.LocalEndpoint).Port;
        portProbe.Stop();
        using var listener = new HttpListener();
        var endpoint = $"http://127.0.0.1:{port}";
        listener.Prefixes.Add(endpoint + "/");
        listener.Start();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var received = new ConcurrentDictionary<string, string>();
        var requests = new ConcurrentBag<Activity>();
        using var requestListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "System.Net.Http",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                if (activity.TagObjects.Any(tag => tag.Value?.ToString()?.Contains("/probe") == true)) requests.Add(activity);
            }
        };
        ActivitySource.AddActivityListener(requestListener);
        var collected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var server = Task.Run(async () =>
        {
            try
            {
                while (!stop.IsCancellationRequested)
                {
                    var context = await listener.GetContextAsync().WaitAsync(stop.Token);
                    Assert.Equal("test-auth", context.Request.Headers["x-api-key"]);
                    using var bytes = new MemoryStream();
                    await context.Request.InputStream.CopyToAsync(bytes, stop.Token);
                    received[context.Request.Url!.AbsolutePath] = Encoding.UTF8.GetString(bytes.ToArray());
                    context.Response.StatusCode = 200;
                    context.Response.ContentType = "application/x-protobuf";
                    context.Response.ContentLength64 = 0;
                    context.Response.Close();
                    if (received.Keys.Contains("/v1/logs") && received.Keys.Contains("/v1/traces") && received.Keys.Contains("/v1/metrics"))
                        collected.TrySetResult();
                }
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        }, ct);
        try
        {
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { ApplicationName = "Fatewake.TelemetryTest" });
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OTEL_EXPORTER_OTLP_ENDPOINT"] = endpoint,
                ["OTEL_EXPORTER_OTLP_PROTOCOL"] = "http/protobuf",
                ["OTEL_EXPORTER_OTLP_HEADERS"] = "x-api-key=test-auth"
            });
            builder.AddObservability();
            using (var host = builder.Build())
            {
                await host.StartAsync(ct);
                var logger = host.Services.GetRequiredService<ILogger<ObservabilityTests>>();
                using (OperationTelemetry.Start("test.export", logger))
                    logger.LogInformation("Typed logging export probe");
                using var http = new HttpClient();
                http.DefaultRequestHeaders.Add("x-api-key", "test-auth");
                using var probe = await http.GetAsync(endpoint + "/probe?token=private-verification-secret", ct);
                Assert.Equal(HttpStatusCode.OK, probe.StatusCode);
                Assert.True(host.Services.GetRequiredService<TracerProvider>().ForceFlush());
                Assert.True(host.Services.GetRequiredService<MeterProvider>().ForceFlush());
                await host.StopAsync(ct);
            }
            await collected.Task.WaitAsync(TimeSpan.FromSeconds(20), ct);
            Assert.All(new[] { "/v1/logs", "/v1/traces", "/v1/metrics" },
                path => Assert.Contains("Fatewake.TelemetryTest", received[path]));
            Assert.Contains("Typed logging export probe", received["/v1/logs"]);
            Assert.Contains(nameof(ObservabilityTests), received["/v1/logs"]);
            Assert.Contains("test.export", received["/v1/traces"]);
            Assert.Contains("fatewake.operation.duration", received["/v1/metrics"]);
            Assert.NotEmpty(requests);
            Assert.All(requests, request => Assert.DoesNotContain(request.TagObjects,
                tag => tag.Value?.ToString()?.Contains("private-verification-secret") == true));
            Assert.DoesNotContain("private-verification-secret", received["/v1/traces"]);
        }
        finally
        {
            stop.Cancel();
            await server;
            listener.Stop();
        }
    }
}
