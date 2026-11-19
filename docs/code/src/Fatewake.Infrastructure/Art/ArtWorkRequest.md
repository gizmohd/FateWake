# ArtWorkRequest

## Purpose
Immutable request used to identify and generate a canonical visual asset. VisualFingerprint is the durable idempotency identity for the logical art job.

## Workflow
Artwork is treated as a durable asset-creation operation. Existing approved assets should be resolved before provider generation. After a master exists, independent derivative work may execute concurrently on different worker replicas. Validation is a fan-in barrier and finalization occurs only after required outputs succeed.

## Kubernetes and concurrency
No step assumes process affinity. Inputs and intermediate identifiers must reference PostgreSQL/shared object storage rather than pod-local state. Handlers are retryable and must be idempotent because leases can expire and RabbitMQ delivery is at least once.

## Cost and provenance
Provider generation is the expensive boundary. Generation handlers must preserve exact prompt/provider/model/reference provenance and must not repeat a provider call when a valid persisted master already exists for the canonical fingerprint.

## Ownership
Shared authored assets leave `IsPrivate` false and can be reused by all players. Survivor-specific artwork sets `IsPrivate` so finalization records its owning account; private assets are only eligible for reuse by that account and are delivered with private cache directives.

Predictive requests use authored scene keys only and may be promoted when the player selects an action. A private request requires `AccountId`; its durable idempotency key is scoped to that account so one player's request cannot absorb another's work.
