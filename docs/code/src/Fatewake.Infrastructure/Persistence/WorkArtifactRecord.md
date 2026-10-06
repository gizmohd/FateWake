# WorkArtifactRecord

## Purpose
PostgreSQL row containing one named JSON value shared by steps in the same durable job. The unique JobId/Key pair gives every intermediate result a stable cross-pod address.

## Distributed usage
Artifacts are the handoff mechanism between steps that may execute on different Linux or Windows worker replicas. Large binary data is not stored here; artifacts carry JSON metadata and shared-storage keys. PostgreSQL remains authoritative.

## Idempotency
Artifact keys are stable within a job. Retried handlers should converge on the same logical value and must not depend on in-memory state from a prior attempt.
