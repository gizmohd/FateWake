# ArtWorkJobFactory

## Purpose
Creates the artwork dependency graph: resolve → generate master → parallel WebP/PNG/metadata → validate → finalize. PostgreSQL persists graph state and RabbitMQ only accelerates dispatch.

## Workflow
Artwork is treated as a durable asset-creation operation. Existing approved assets should be resolved before provider generation. After a master exists, independent derivative work may execute concurrently on different worker replicas. Validation is a fan-in barrier and finalization occurs only after required outputs succeed.

## Kubernetes and concurrency
No step assumes process affinity. Inputs and intermediate identifiers must reference PostgreSQL/shared object storage rather than pod-local state. Handlers are retryable and must be idempotent because leases can expire and RabbitMQ delivery is at least once.

## Cost and provenance
Provider generation is the expensive boundary. Generation handlers must preserve exact prompt/provider/model/reference provenance and must not repeat a provider call when a valid persisted master already exists for the canonical fingerprint.
