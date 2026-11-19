# Asset Validation

Pathweaver treats artwork keys as versioned product dependencies. `tools/Fatewake.AssetValidator` scans application source for referenced artwork keys, compares allowlisted scene-layer metadata with the versioned SQL seed artifact, and checks for runtime prompt-file reads.

The report distinguishes:
- referenced artwork keys;
- approved/render assets currently present;
- placeholders (referenced but no image file yet);
- server-owned scene/layer metadata and seed entries;
- missing or orphan SQL prompt seeds (validation failure);
- malformed seed records and local prompt-file fallback (validation failure);
- renderable assets currently present.

Missing rendered artwork remains an allowed placeholder. Missing prompt Markdown is not an error: a legitimate SQL prompt miss queues durable authoring when enabled, and otherwise returns an explicit disabled state. Seed coverage is required for the currently authored Day 1 catalog; later allowlisted content can rely on generate-on-miss instead of a local Markdown file.

Run locally:

```bash
dotnet run --project tools/Fatewake.AssetValidator/Fatewake.AssetValidator.csproj -- .
```

GitHub Actions runs restore, a warnings-as-errors Release build, tests against isolated PostgreSQL 17 and RabbitMQ services, and API/Web publishing on pushes and pull requests to main and on manual dispatch. The broker service runs the work-wakeup integration test; the running-AppHost artwork pipeline test is opt-in locally. Artwork validation runs in a separate job. Test results (TRX) and published applications are retained as artifacts for 14 days.

The database-backed tests require `FATEWAKE_TEST_CONNECTION` locally; without it, their database assertions do not run. Use a disposable database because these tests delete and recreate it. CI supplies the connection automatically.

The validator checks scene-beat composition keys and artwork-layer keys, not scene identifiers or game-rules versions. It does not claim that a SQL seed guarantees runtime prompt availability after a database edit; the API reports missing, pending, disabled and failed prompt states explicitly.
