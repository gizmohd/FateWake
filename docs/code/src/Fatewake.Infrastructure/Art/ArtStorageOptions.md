# ArtStorageOptions

Configures binary-art storage used by API and Worker. `Provider` defaults to PostgreSQL. `RootPath` is optional and is used as a read-only source for verified migration/backfill; filesystem writes are available only when `Provider` is explicitly set to `FileSystem`.
