# ArtFinalizeWorkStepHandler

## Purpose
Transactionally creates or reuses an approved asset and switches only survivor identities still waiting for the exact job and fingerprint.

## Safety and idempotency
The distributed pipeline uses durable PostgreSQL artifacts and shared binary storage. Final activation is conditional on the survivor still waiting for the same work job and visual fingerprint, preventing stale background generation from replacing a newer player choice. A prompt-backed replacement promotes its pinned SQL revision only when it remains the prompt's requested revision and the same job is still the requested work; a failed or superseded replacement leaves the current revision and approved asset untouched.
