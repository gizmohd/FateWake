# SqlDataProtectionTests

Creates unique test-owned PostgreSQL databases using `FATEWAKE_TEST_CONNECTION` server credentials, applies migrations, and checks encrypted and development-plaintext key rings. Independent providers must unprotect earlier data after restart; different application names must not. Certificate-wrapped key XML is persisted without exposing plaintext material, and missing production certificate configuration is rejected. Test databases are deleted in `finally`; no application database is modified.
