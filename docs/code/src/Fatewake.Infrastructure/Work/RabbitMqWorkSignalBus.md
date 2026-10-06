# RabbitMqWorkSignalBus

Publication is measured as messaging.publish with payload-free operation tracing and latency. Failures mark the span and propagate to existing typed-log recovery paths; no broker secrets or message body are added to telemetry.

## Purpose
RabbitMQ implementation of work signaling. Declares durable direct exchange/queues and publishes persistent lightweight step notifications. Consumers must still claim PostgreSQL leases before executing work.

Connections are opened asynchronously on first publication, not during service construction. Publications are serialized because RabbitMQ channels cannot safely be shared by concurrent publishers. Caller-side failures are logged after durable state has committed; workers recover through PostgreSQL polling.

## Usage
This type belongs to Fatewake's distributed work subsystem and is designed for horizontally scaled Linux or Windows Kubernetes workloads. See the XML documentation on the source type for its direct code contract.

## Distributed behavior
PostgreSQL is authoritative for durable work. RabbitMQ is an acceleration/wakeup mechanism only. Pods are disposable, duplicate delivery is expected, and handlers must preserve idempotency.

## Operational considerations
Configure queue concurrency according to CPU, memory, provider rate limits, native-library pressure, and cost. Do not rely on pod-local ownership or process lifetime for correctness.
