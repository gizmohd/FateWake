# StartSessionRequest

## Purpose
API contract for starting or resuming a survivor session. BroadRegion is deliberately coarse and must never be replaced with precise real-world location.

## Usage
This record is bound from an ASP.NET Core API request and passed to application/domain services. It contains transport data only and does not itself mutate game state.

## Validation and security
Callers must be treated as untrusted. Authoritative identifiers and actions are validated/resolved by server-side systems. Never infer additional permissions or canonical facts merely because a field is present in the request.
