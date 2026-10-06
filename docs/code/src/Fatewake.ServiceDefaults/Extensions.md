# Extensions

`Extensions.AddServiceDefaults` configures service discovery and shared Serilog/OpenTelemetry. AddObservability configures telemetry without discovery for CLI tools; AddFatewakeObservability supports Aspire's builder through its IServiceCollection.

Call it from each hosted application's builder setup before building the host. The extension returns the same builder to support the standard Aspire configuration chain.

Serilog backs injected Microsoft ILogger categories, console output, configuration-controlled levels/scopes, and correlated OTLP logs. OpenTelemetry owns ASP.NET/HttpClient/Npgsql/operation traces and HTTP/runtime/database/operation metrics. Exporters are installed only for configured common/signal-specific endpoints, with gRPC or HTTP/protobuf and protected collector headers. Host disposal flushes exporters. Sink network calls suppress instrumentation; raw query strings are removed from enriched spans. No duplicate OpenTelemetry logging provider is registered.

Root traces use configurable parent-based Telemetry:TraceSampleRatio (0-1, default 1). Invalid protocols or sampling ratios fail configuration; Serilog internal sink diagnostics go to stderr.

See docs/OBSERVABILITY.md for the permanent instrumentation/performance policy. Pure helper/DTO/GameEngine methods are instrumented at their runtime boundary, not through invasive dependencies.
