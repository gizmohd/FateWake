# ObservabilityTests

Tests span parenting/error type privacy, bounded histogram dimensions, and idempotent scope disposal. A real loopback HTTP/protobuf OTLP collector receives typed Microsoft logs backed by Serilog, operation traces, and latency/runtime metrics with authenticated headers and consistent resource identity. Hosts/listeners are disposed; tests require no external collector. Metrics filter their own fixed operation name to remain isolated from concurrent tests.

A real outbound HTTP request with a credential-like query verifies that raw token values do not reach exported span attributes.
