# DevelopmentRegistrationTests

Checks that only Development skips mailbox proof and that required profile details survive both immediate registration and Production/Staging proof plus resend. Tests use synthetic international names and locations and verify trimmed values, canonical profile ownership, completion timestamps, and removal of pending profile data after proof consumption.

Non-database cases exercise rejection of each missing, blank, oversized, or control-character-bearing profile field before password hashing, persistence, or mail delivery. Database cases require an isolated `FATEWAKE_REGISTRATION_TEST_CONNECTION`; never use an application database.

The profile-completion telemetry test verifies the fixed operation name and an allowlist of span/metric tag keys; submitted personal details must not become telemetry dimensions.
