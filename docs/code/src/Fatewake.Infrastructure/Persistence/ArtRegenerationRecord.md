# ArtRegenerationRecord

Durable state for an artwork that was generated with fallback provider quality and should be regenerated with the preferred provider.

## Lifecycle

Rows begin in `Pending`, move to `Processing` when a replacement work job is queued, and may be marked `Completed` or `Cancelled` by an administrator or a future completion integration.
