# NullWorkSignalBus

## Purpose
No-op signal implementation used when RabbitMQ is unavailable or intentionally disabled. PostgreSQL polling remains sufficient for correctness.

## Usage
This type belongs to Fatewake's distributed work subsystem and is designed for horizontally scaled Linux or Windows Kubernetes workloads. See the XML documentation on the source type for its direct code contract.

## Distributed behavior
PostgreSQL is authoritative for durable work. RabbitMQ is an acceleration/wakeup mechanism only. Pods are disposable, duplicate delivery is expected, and handlers must preserve idempotency.

## Operational considerations
Configure queue concurrency according to CPU, memory, provider rate limits, native-library pressure, and cost. Do not rely on pod-local ownership or process lifetime for correctness.
