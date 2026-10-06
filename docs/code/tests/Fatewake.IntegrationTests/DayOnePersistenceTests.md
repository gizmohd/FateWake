# DayOnePersistenceTests

Local account coverage uses the same disposable PostgreSQL database and serialized test class as migration tests. It exercises registration validation, normalized email uniqueness and atomic rollback, versioned password hashes and rehash upgrades, durable failure lockout/expiry/reset, account-owned sessions separate from guests, context restart, and migration rollback preserving account/survivor data.

Concurrent bad passwords must preserve every failure count, and concurrent first visits must create one account survivor. In-process API tests cover actual registration/login bearer sessions, anonymous identity rejection, foreign-account denial on every gameplay route, invalid-token rejection rather than anonymous downgrade, and successful authorized progress/actions.

Email/provider coverage includes local proof and password confirmation, matching-account linking, Microsoft/unverified Google mailbox challenges, single-use/expired tokens, resend, unsolicited pre-registration, initial backup-password setup, signed provider-token issuer/audience/expiry rejection, real OIDC code/PKCE/nonce callbacks with a fake provider, and antiforgery-protected browser registration/verification/setup/logout. No real provider credentials or external email delivery are needed.

The email migration also preserves pre-existing local account IDs and password hashes, backfills ownership as unverified, and enables login only after mailbox proof. Rollback preserves those account/credential records.

These PostgreSQL-backed tests verify atomic persistence of action outcomes and migration behavior. They require `FATEWAKE_TEST_CONNECTION` and use a disposable database because setup deletes and recreates it.

The durable-work migration case exercises real PostgreSQL leasing, artifacts, job completion, and rollback/reapplication while preserving the original survivor tables.
