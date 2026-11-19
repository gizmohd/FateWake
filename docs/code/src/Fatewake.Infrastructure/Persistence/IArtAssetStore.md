# IArtAssetStore

Persistence boundary for exact approved-fingerprint and explicit key/version lookups. Both lookup forms admit shared assets and, when an account scope is provided, assets owned by that account; an unscoped lookup never returns private art. Generation handlers must check this boundary before invoking providers.
