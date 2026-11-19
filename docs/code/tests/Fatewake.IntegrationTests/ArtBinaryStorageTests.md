# ArtBinaryStorageTests

Exercises the PostgreSQL binary-storage implementation's writes, replacement, content hashing, and reads with an isolated EF test database. It also checks that registered filesystem masters and derivatives backfill once, verify their bytes, and leave the legacy source intact, while missing legacy files do not block startup and can be regenerated through scene resolution.
