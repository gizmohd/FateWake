# DayOneResolveRequest

## Purpose
API contract for deterministic Day One action resolution. IdempotencyKey protects consequential submission from duplicate client/network delivery.

## Usage
This record is bound from an ASP.NET Core API request and passed to application/domain services. It contains transport data only and does not itself mutate game state.

## Validation and security
Callers must be treated as untrusted. Authoritative identifiers and actions are validated/resolved by server-side systems. Never infer additional permissions or canonical facts merely because a field is present in the request.
