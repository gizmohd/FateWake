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

GitHub Actions runs restore, Release build, tests and asset validation on pushes and pull requests to main.
