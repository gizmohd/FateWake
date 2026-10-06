# DayOnePersistenceTests

These PostgreSQL-backed tests verify atomic persistence of action outcomes and migration behavior. They require `FATEWAKE_TEST_CONNECTION` and use a disposable database because setup deletes and recreates it.
