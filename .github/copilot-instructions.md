# Fatewake — Copilot Instructions

Fatewake is a persistent narrative survival/social strategy game (mobile-first web/PWA). The game engine decides what happens; AI decides how it is told.

## Build, test, lint

Requires .NET 10 SDK (`global.json` pins `10.0.100`).

```bash
dotnet restore Fatewake.slnx
dotnet build Fatewake.slnx --configuration Release
dotnet test Fatewake.slnx --configuration Release
```

Run a single test project or test:
```bash
dotnet test tests/Fatewake.GameEngine.Tests/Fatewake.GameEngine.Tests.csproj
dotnet test tests/Fatewake.GameEngine.Tests/Fatewake.GameEngine.Tests.csproj --filter "FullyQualifiedName~DayOneGameEngineTests.SomeTest"
```

`Fatewake.IntegrationTests` requires PostgreSQL via the `FATEWAKE_TEST_CONNECTION` env var (e.g. `Host=localhost;Port=5432;Database=fatewake_tests;Username=postgres`). Without it, database-backed assertions are skipped; use a disposable database since tests delete/recreate it. CI (`.github/workflows/validate.yml`) runs a PostgreSQL 17 service automatically.

Asset/artwork contract validation:
```bash
dotnet run --project tools/Fatewake.AssetValidator/Fatewake.AssetValidator.csproj -- .
```
This checks that every referenced artwork key has a prompt file (missing prompts fail the build); missing rendered images are allowed as placeholders. Runs as a separate CI job.

`Directory.Build.props` sets `TreatWarningsAsErrors=true` and `net10.0` for all projects, so build warnings fail the build.

## Architecture

Solution (`Fatewake.slnx`) layout:
- `src/Fatewake.AppHost` — .NET Aspire orchestration (local dev: web/API + PostgreSQL).
- `src/Fatewake.ServiceDefaults` — shared Aspire/telemetry/health-check wiring.
- `src/Fatewake.GameEngine` — deterministic game core (Events, Choices, Consequences, Characters, Relationships, Resources, WorldState). **Must not depend on** Aspire, EF Core, PostgreSQL, HTTP, or any AI provider.
- `src/Fatewake.AI` — Narrator/Dialogue/ContentGuardrails; renders authoritative GameEngine results into presentation text. Does not decide game outcomes.
- `src/Fatewake.Api` — application/use-case orchestration.
- `src/Fatewake.Web` — Blazor Web App/PWA presentation.
- `src/Fatewake.Infrastructure` — persistence (PostgreSQL) and external integrations.
- `src/Fatewake.Worker` — background processing.
- `tools/Fatewake.AssetValidator` — standalone CLI that scans source for artwork key references vs. prompts/assets.
- `tests/` mirrors `src/` (`Fatewake.GameEngine.Tests`, `Fatewake.Web.Tests`, `Fatewake.IntegrationTests`).

Dependency direction flows one way: GameEngine (deterministic core) ← Infrastructure/AI ← Api/orchestration ← Web. Keep game-rules/state resolution testable in isolation from persistence, AI, and transport concerns.

PostgreSQL is the single authoritative datastore (no polyglot persistence by default). Use relational tables for well-understood entities, JSONB for flexible/evolving event & AI metadata, append-only records for consequential history, and recursive queries/edge tables for graph-like traversal (social relationships, causal decision→event→Wake chains, information provenance). Add Redis, pgvector, or a graph DB only when a concrete, measured need justifies it.

Character appearance/visual assets are durable canonical state, not regenerated per use: Base Identity → Appearance Configuration → Equipment/Loadout → Condition/Injuries → Scene/Pose/Camera → Rendered Asset. Each normalized appearance combination gets a deterministic fingerprint; always do an exact approved-asset lookup by fingerprint before calling an AI generation provider. Approved assets remain reusable indefinitely (switching configurations back resolves the prior approved asset; character death does not invalidate reusable artwork).

Full design docs live under `docs/` (GDD, WORLD, GAMEPLAY, WAKE-SYSTEM, AI-DESIGN, ARCHITECTURE, DATA-MODEL, TIMELINES-AND-CONVERGENCE) and `docs/decisions/DECISION-LOG.md` — these are the design source of truth; check them before making gameplay/architecture decisions that aren't obvious from code.

## Conventions

- **Cross-platform first**: server apps and tools must run on both Linux and Windows. No OS-specific path separators, shell assumptions, or native-only dependencies without cross-platform support. Storage keys are portable logical paths translated by platform-specific adapters.
- **One declared type per C# file**, file name matches the type name. Nested private types should be extracted unless genuinely coupled. `Program.cs` top-level entry points are exempt; other types declared alongside them are not.
- **Every public/internal type requires a companion Markdown doc** at `docs/code/<project-relative-source-path-without-.cs>.md` (e.g. `src/Fatewake.Infrastructure/Work/WorkJobBuilder.cs` → `docs/code/src/Fatewake.Infrastructure/Work/WorkJobBuilder.md`). The type's XML doc comment must link to it via `<see href="...">` with a correct relative `../` path.
- XML doc comments on public/internal members should explain intent, invariants, lifecycle, concurrency/idempotency, and side effects — not restate the signature.
- Documentation changes ship in the same commit as the source behavior change they describe.
- When materially modifying an existing file that predates these standards, bring it into compliance rather than adding more undocumented structure.
- Generated code, EF migrations, and other framework-generated artifacts are exempt from the one-type-per-file/companion-doc rules.
