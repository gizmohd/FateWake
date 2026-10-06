using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Fatewake.Observability;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Exporter;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.OpenTelemetry;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Provides shared service configuration for Fatewake hosted applications.
/// </summary>
/// <see href="../../docs/code/src/Fatewake.ServiceDefaults/Extensions.md">Extensions documentation</see>
public static class Extensions
{
    /// <summary>
    /// Adds common service discovery and HTTP client defaults to an application builder.
    /// </summary>
    /// <typeparam name="TBuilder">The concrete host application builder type.</typeparam>
    /// <param name="builder">The builder to configure.</param>
    /// <returns>The same builder, configured with the shared service defaults.</returns>
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.AddObservability();
        builder.Services.AddServiceDiscovery();
        builder.Services.ConfigureHttpClientDefaults(http => http.AddServiceDiscovery());
        return builder;
    }

    /// <summary>Configures Serilog-backed Microsoft logging, correlated OTLP logs, distributed traces, and runtime/operation metrics.</summary>
    /// <typeparam name="TBuilder">Concrete application builder.</typeparam>
    /// <param name="builder">Host builder; OTLP uses standard environment configuration including Aspire headers.</param>
    /// <returns>The configured builder.</returns>
    public static TBuilder AddObservability<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.ClearProviders();
        builder.Services.AddFatewakeObservability(builder.Configuration, builder.Environment.ApplicationName);
        return builder;
    }

    /// <summary>Registers common telemetry for host builders including Aspire's distributed application builder.</summary>
    /// <param name="services">Application services.</param>
    /// <param name="configuration">Standard Serilog and OTLP configuration.</param>
    /// <param name="serviceName">Stable application/service identity.</param>
    /// <returns>The configured services.</returns>
    public static IServiceCollection AddFatewakeObservability(this IServiceCollection services, IConfiguration configuration, string serviceName)
    {
        Serilog.Debugging.SelfLog.Enable(TextWriter.Synchronized(Console.Error));
        var traceRatio = configuration.GetValue("Telemetry:TraceSampleRatio", 1.0);
        if (!double.IsFinite(traceRatio) || traceRatio is < 0 or > 1)
            throw new InvalidOperationException("Telemetry:TraceSampleRatio must be between 0 and 1.");
        services.AddLogging(logging => logging.ClearProviders());
        services.AddSerilog((provider, logger) =>
        {
            logger.MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
                .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
                .MinimumLevel.Override("Aspire", LogEventLevel.Information)
                .ReadFrom.Configuration(configuration)
                .ReadFrom.Services(provider)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("service.name", serviceName)
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{service.name}] [{TraceId}/{SpanId}] {SourceContext}: {Message:lj}{NewLine}{Exception}");
            if (!string.IsNullOrWhiteSpace(configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]) ||
                !string.IsNullOrWhiteSpace(configuration["OTEL_EXPORTER_OTLP_LOGS_ENDPOINT"]))
                logger.WriteTo.OpenTelemetry(options =>
                {
                    options.Endpoint = configuration["OTEL_EXPORTER_OTLP_LOGS_ENDPOINT"] ?? configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]!;
                    var protocol = configuration["OTEL_EXPORTER_OTLP_LOGS_PROTOCOL"] ?? configuration["OTEL_EXPORTER_OTLP_PROTOCOL"];
                    options.Protocol = protocol switch
                    {
                        null or "grpc" => OtlpProtocol.Grpc,
                        "http/protobuf" => OtlpProtocol.HttpProtobuf,
                        _ => throw new InvalidOperationException($"Unsupported OTLP log protocol '{protocol}'.")
                    };
                    var headers = configuration["OTEL_EXPORTER_OTLP_LOGS_HEADERS"] ?? configuration["OTEL_EXPORTER_OTLP_HEADERS"];
                    if (!string.IsNullOrWhiteSpace(headers))
                        options.Headers = headers.Split(',').Select(header => header.Split('=', 2))
                            .ToDictionary(header => header[0].Trim(), header => Uri.UnescapeDataString(header[1].Trim()));
                    options.ResourceAttributes = new Dictionary<string, object> { ["service.name"] = serviceName };
                    options.IncludedData = IncludedData.TraceIdField | IncludedData.SpanIdField | IncludedData.MessageTemplateTextAttribute;
                    options.OnBeginSuppressInstrumentation = OpenTelemetry.SuppressInstrumentationScope.Begin;
                });
        });
        var telemetry = services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(traces => traces
                .SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(traceRatio)))
                .AddSource(OperationTelemetry.SourceName, "Npgsql")
                .AddAspNetCoreInstrumentation(options =>
                {
                    options.Filter = context => !context.Request.Path.StartsWithSegments("/health");
                    options.EnrichWithHttpRequest = (activity, _) =>
                    {
                        activity.SetTag("url.query", null);
                        activity.SetTag("url.full", null);
                    };
                })
                .AddHttpClientInstrumentation(options =>
                {
                    options.EnrichWithHttpRequestMessage = (activity, request) =>
                    {
                        activity.SetTag("url.full", request.RequestUri?.GetLeftPart(UriPartial.Path));
                        activity.SetTag("url.query", null);
                    };
                }))
            .WithMetrics(metrics => metrics.AddMeter(OperationTelemetry.SourceName, "Npgsql")
                .AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddRuntimeInstrumentation());
        if (!string.IsNullOrWhiteSpace(configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]) ||
            !string.IsNullOrWhiteSpace(configuration["OTEL_EXPORTER_OTLP_TRACES_ENDPOINT"]))
            telemetry.WithTracing(traces => traces.AddOtlpExporter(options => ConfigureOtlp(options, configuration, "TRACES")));
        if (!string.IsNullOrWhiteSpace(configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]) ||
            !string.IsNullOrWhiteSpace(configuration["OTEL_EXPORTER_OTLP_METRICS_ENDPOINT"]))
            telemetry.WithMetrics(metrics => metrics.AddOtlpExporter(options => ConfigureOtlp(options, configuration, "METRICS")));
        return services;
    }

    private static void ConfigureOtlp(OtlpExporterOptions options, IConfiguration configuration, string signal)
    {
        var protocol = configuration[$"OTEL_EXPORTER_OTLP_{signal}_PROTOCOL"] ?? configuration["OTEL_EXPORTER_OTLP_PROTOCOL"];
        options.Protocol = protocol switch
        {
            null or "grpc" => OtlpExportProtocol.Grpc,
            "http/protobuf" => OtlpExportProtocol.HttpProtobuf,
            _ => throw new InvalidOperationException($"Unsupported OTLP {signal} protocol '{protocol}'.")
        };
        var endpoint = configuration[$"OTEL_EXPORTER_OTLP_{signal}_ENDPOINT"];
        if (endpoint is null && configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] is {} common)
            endpoint = options.Protocol == OtlpExportProtocol.HttpProtobuf ? common.TrimEnd('/') + "/v1/" + signal.ToLowerInvariant() : common;
        if (endpoint is not null) options.Endpoint = new Uri(endpoint);
        options.Headers = configuration[$"OTEL_EXPORTER_OTLP_{signal}_HEADERS"] ?? configuration["OTEL_EXPORTER_OTLP_HEADERS"];
    }
}
