# Asset Validation

Fatewake treats artwork keys as versioned product dependencies. `tools/Fatewake.AssetValidator` scans application source for referenced artwork keys and compares them with prompt files and renderable assets.

The report distinguishes:
- referenced artwork keys;
- approved/render assets currently present;
- placeholders (referenced but no image file yet);
- missing prompts (validation failure);
- orphan prompts (informational).

Missing artwork is allowed during development because the UI has a deliberate placeholder fallback. Missing prompt coverage is not allowed: every referenced visual must remain reproducible.

Run locally:

```bash
dotnet run --project tools/Fatewake.AssetValidator/Fatewake.AssetValidator.csproj -- .
```

GitHub Actions runs restore, a warnings-as-errors Release build, tests against isolated PostgreSQL 17 and RabbitMQ services, and API/Web publishing on pushes and pull requests to main and on manual dispatch. The broker service runs the work-wakeup integration test; the running-AppHost artwork pipeline test is opt-in locally. Artwork validation runs in a separate job. Test results (TRX) and published applications are retained as artifacts for 14 days.

The database-backed tests require `FATEWAKE_TEST_CONNECTION` locally; without it, their database assertions do not run. Use a disposable database because these tests delete and recreate it. CI supplies the connection automatically.

The validator checks scene-beat composition keys and artwork-layer keys, not scene identifiers or game-rules versions. Missing prompt coverage fails validation; missing render assets remain allowed placeholders.
