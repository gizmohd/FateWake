# ArtValidationArtifact

## Purpose
Validation result that gates canonical approval.

## Safety and idempotency
The distributed pipeline uses durable PostgreSQL artifacts and shared binary storage. Final activation is conditional on the survivor still waiting for the same work job and visual fingerprint, preventing stale background generation from replacing a newer player choice.
