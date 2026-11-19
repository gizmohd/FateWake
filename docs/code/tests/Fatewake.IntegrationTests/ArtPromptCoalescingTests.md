# ArtPromptCoalescingTests

Requires `FATEWAKE_ART_TEST_CONNECTION` to target the disposable PostgreSQL artwork database. It places one seeded prompt into a missing/disabled state, races eight independent API scopes, and verifies a single durable `art.prompt-author` job and queue step. No provider is called; the test restores the SQL prompt head and removes its temporary work record.
