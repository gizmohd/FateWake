# WorkLease

## Purpose
Immutable claim result handed to a worker. The lease token must accompany completion, heartbeat, or failure transitions so stale workers cannot mutate a re-leased step.

## Usage
Used by Fatewake's durable distributed-work subsystem. PostgreSQL is the source of truth; RabbitMQ only reduces dispatch latency.

## Concurrency and Kubernetes
Multiple Linux or Windows worker processes/pods may operate concurrently. No process owns a job by identity alone. Active execution is fenced by a bounded database lease and unique lease token. Expired leases are recoverable by another worker.

## Failure and idempotency
At-least-once execution is expected. Step handlers must be idempotent and external side effects should use durable idempotency keys or equivalent provider guarantees. A failed broker notification cannot strand work because polling rediscovers eligible rows.
