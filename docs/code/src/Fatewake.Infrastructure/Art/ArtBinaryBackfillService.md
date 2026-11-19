# ArtBinaryBackfillService

Imports every registered PNG master and derivative referenced by `art_asset` or `art_asset_derivative` from the configured legacy root into PostgreSQL. It also checks the previous `Fatewake.Web/wwwroot/art` location used by older local setups, so changing the default root does not strand registered staging files. It verifies bytes against metadata, skips already-imported matching rows, and verifies each stored hash after writing. Missing files are logged and skipped rather than blocking API startup; scene resolution queues durable replacement generation when it detects an incomplete approved asset.

The service is restartable and never deletes or modifies legacy files. Missing or inconsistent files fail startup rather than silently publishing incomplete artwork.
