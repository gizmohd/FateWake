# SurvivorVisualIdentityRecord

## Purpose
Keeps the currently active immutable art asset separate from a pending background request, allowing gameplay to continue on default artwork until generation is approved.

## Lifecycle
The active asset remains renderable while a requested work job/fingerprint is pending. Successful finalization clears the request and activates the approved asset. Reuse can point a new survivor at an existing immutable approved identity without generation.
