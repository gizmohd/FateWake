# SqlDataProtectionTests

Creates unique test-owned PostgreSQL databases using `FATEWAKE_TEST_CONNECTION` server credentials and applies migrations. Both Web and API cases protect a payload using the SQL certificate encryptor, then recover it through an independent read-only provider with automatic key generation disabled. This exercises the framework's actual serialized-decryptor activation path, not direct constructor injection. Different application names must remain isolated.

The test verifies that encrypted key rows are unchanged and no certificate is replaced during recovery. It also checks rejection of explicitly empty or short wrapping passwords. The password in the test is a public disposable fixture value, not an application secret. Test databases are deleted in `finally`; no application database is modified.
