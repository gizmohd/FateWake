# FatewakeDbContext

Authoritative PostgreSQL EF model for accounts, gameplay, artwork, work queues, corpus data, and OpenIddict's isolated `oidc` schema. Implements `IDataProtectionKeyContext` for the shared `data_protection_keys` table. Treat key XML as secret material: it must never be returned through administration APIs or included in telemetry.

The API owns additive migrations before readiness. Web uses this context only for Data Protection persistence; its replicas must wait for API readiness before accepting cookie/antiforgery requests.
