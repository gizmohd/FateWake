# ArtBinaryRecord

Persists one artwork binary in PostgreSQL, separate from the searchable `art_asset` metadata. The stable storage key lets existing artwork jobs and derivative metadata address the binary through `IArtBinaryStorage`.

`Bytes` is the authoritative `bytea` payload. `ContentHash` and `ByteSize` are recorded with it so migrations, delivery validators, and storage checks can verify that the binary matches its metadata.
