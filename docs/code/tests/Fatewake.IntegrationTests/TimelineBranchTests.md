# TimelineBranchTests

Disposable PostgreSQL coverage for pre-decision restoration, distinct outcomes, frozen history, branch switching/resume, branch-of-branch lineage, exact retry identity, fresh openings, owner/shared/stale rejection, resolution/fork failure rollback, concurrent forks, progression/fork version races, development-reset descendant protection and legacy migration/current-facts backfill without fabricated history. Pure cases check JSONB-stable integrity and malformed canonical-state rejection. Failure injection also verifies payload-free error telemetry.

Uses only `FATEWAKE_TIMELINE_TEST_CONNECTION` with a database name containing `timeline`; never point it at real gameplay data. Each database-backed case recreates its disposable database.
