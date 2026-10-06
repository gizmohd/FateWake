# Logging, telemetry, and performance policy

This is a permanent repository requirement for existing runtime operations and every future new or modified file/method. Observability is designed at operation boundaries, not as indiscriminate entry/exit logging on every helper.

## Runtime logging

- Every host uses `AddServiceDefaults`, or `AddObservability` for non-service tools; AppHost uses `AddFatewakeObservability`. Serilog is the backend, not the library-facing API.
- Runtime services receive `ILogger<T>` from Microsoft.Extensions.Logging. Use structured constant message templates, named properties, appropriate levels, and scopes. Do not use global `Serilog.Log` or create per-library providers/sinks.
- Log meaningful lifecycle transitions and recoverable failures. Use Debug for operation timing; Information for business transitions; Warning for recoverable outages/rejected operations; Error for failures requiring investigation. Do not log every poll, getter, collection iteration, or method call.
- Never log passwords, password hashes, email-verification tokens/links, bearer tokens, authorization/cookie headers, SMTP secrets, provider response bodies, raw player input, resolved prompts, or SQL parameter values. Do not enable EF sensitive-data logging or verbose HTTP URL logging in deployed applications.
- Preserve explicit failures; instrumentation must not introduce fallback success, swallow exceptions, or change canonical game state. Serilog providers are host-owned and flush/dispose on orderly host shutdown.

## Traces and metrics

Shared hosting configuration collects ASP.NET inbound HTTP, outbound HttpClient, Npgsql database tracing/metrics, .NET runtime metrics, and `Fatewake.Operations` spans/metrics. Serilog sends correlated OTLP log records directly, avoiding duplicate Microsoft/OpenTelemetry log providers. Transport instrumentation is suppressed for sink exports to avoid recursion.

`OperationTelemetry.Start("constant.operation", typedLogger)` measures account, session, resolution persistence, artwork ingestion/generation, job creation, publication, and worker execution. API wraps deterministic engine execution rather than adding runtime dependencies inside GameEngine. Static pure AI phrase mapping, DTOs, fingerprints, and presentation helpers need no entry/exit logging; observe their use-case boundary and benchmark expensive pure logic separately.

Scopes must be disposed once the measured operation ends. Error-handling boundaries call `Fail(exception)` before preserving existing exception/notification behavior. This exports exception type only; it does not export message/payload. Cancellation is separate from error. An ordinary scope's `ended` label denotes duration only, not successful business acceptance; rejected credentials and expected validation results are not infrastructure exceptions.

Duration histogram: `fatewake.operation.duration` in seconds. Failure counter: `fatewake.operation.failures`. Labels contain fixed operation and outcome only. Never add user/account/job/step IDs, emails, fingerprints, arbitrary URLs, or payload strings as metric dimensions. Job/step IDs belong in logging scopes, not metrics. Trace names must be fixed. HTTP query strings are removed from enriched spans, and credential-bearing provider response bodies are not included in exceptions.

New application operations must add meaningful spans/timing, use typed logging for actionable events/errors, preserve parent activities across async calls, and have focused tests. New external calls use instrumented HttpClient/database clients. Queue execution is a fresh worker trace: RabbitMQ messages are availability hints, not authoritative execution messages, so do not pretend that their parent context identifies the PostgreSQL lease. A future durable trace-context design must persist context in the job record before promising cross-process job parentage.

## Export configuration

Without an OTLP endpoint, console logging remains enabled and no network exporter is installed. AppHost propagates Aspire's collector settings to child services. Outside Aspire, set:

```powershell
$env:OTEL_EXPORTER_OTLP_ENDPOINT = "http://localhost:4317"
$env:OTEL_EXPORTER_OTLP_PROTOCOL = "grpc"
```

Use `http/protobuf` with port 4318 for an HTTP collector. Common HTTP endpoints append `/v1/logs`, `/v1/traces`, and `/v1/metrics`; signal-specific `OTEL_EXPORTER_OTLP_LOGS_ENDPOINT`, `_TRACES_ENDPOINT`, and `_METRICS_ENDPOINT` are full endpoint URLs. Common or signal-specific `_HEADERS` use comma-separated `key=value` entries; keep collector credentials in protected configuration. All three signals support common/signal-specific protocol settings. HTTPS collectors require trusted certificates; do not disable verification.

Use standard `Serilog:MinimumLevel:Default` and `Serilog:MinimumLevel:Override` configuration for levels. Microsoft framework and HTTP-client informational logs are reduced by default to avoid noise and sensitive URLs. Do not re-enable credential-bearing HTTP request logging. Service identity is the host application name. Libraries do not establish their own service identity/exporters. Existing ASP.NET Data Protection, local account/session behavior and deterministic engine boundaries remain unchanged.

`Telemetry:TraceSampleRatio` selects root-trace sampling from 0 to 1 (default 1) with parent-based propagation; metrics remain available independently. Invalid values fail startup. Export is best-effort and must not change gameplay correctness; Serilog's internal sink/configuration errors are written directly to stderr rather than silently hidden or recursively logged through the failed sink.

## Benchmarking

Production latency metrics and repeatable benchmarks are different tools. Use BenchmarkDotNet for performance-critical CPU/allocation paths, not ad-hoc Stopwatch assertions or benchmarking every method. Add/update representative benchmarks whenever a change affects a hot path; compare Release results on the same machine/runtime and retain artifacts when assessing regressions. Never put logging/exporters in measured deterministic benchmark code.

```powershell
dotnet run --project benchmarks\Fatewake.Benchmarks --configuration Release -- --filter '*'
```

`--job Dry` is only a smoke check (cold-start, one iteration), not a performance baseline. The initial benchmarks cover day-one resolution and appearance fingerprinting. Outputs are ignored under `BenchmarkDotNet.Artifacts`; do not commit machine-specific results. CI builds benchmarks but does not enforce unstable wall-clock thresholds on shared runners.

CI exercises the OTLP transport/privacy tests and Dry benchmark harness on Windows and Linux, retaining smoke results as workflow artifacts rather than committing them.

Review every new or modified file against this policy. DTOs, migrations, simple pure helpers, generated files and deterministic GameEngine methods are deliberate instrumentation exceptions, not gaps to fill with logging. Their relevant operation/benchmark boundary must remain covered.
