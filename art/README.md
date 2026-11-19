# Pathweaver Artwork

Operational scene and layer prompts are versioned in PostgreSQL (`art_prompt` and `art_prompt_revision`). API resolution reads only SQL prompt revisions. It never reads individual prompt Markdown files or substitutes a hard-coded subject when a row is missing.

`prompt-seeds/day1-v1.json` is the immutable one-time import of the existing authored Day 1 prompt set. The API imports only absent identities; database edits and later revisions are never overwritten. This JSON is a seed artifact, not runtime prompt truth. Character sheets in `characters/` and location continuity in `locations/` remain canonical versioned context files. `STYLE-BIBLE.md` remains the shared runtime style and exclusions contract.

Only allowlisted scene layers may request prompts. A valid missing SQL prompt queues coalesced durable `art.prompt` work when `ArtPromptAuthoring:Enabled` is configured; the leased worker uses OpenAI Responses, strict structured output, no tools, and reasoning effort `none`, then saves a revision before creating the successor image job through `ArtWorkJobFactory`. Provider content remains additive and server role, framing, alpha, geometry, style and continuity rules are appended separately.

Prompt edits are immutable. Administrator save-only edits do not change the serving revision or enqueue generation. Explicit regeneration pins the chosen revision and source asset; the prior approved image remains available until validated replacement succeeds. Historic submissions without recorded provenance display as unavailable rather than being reconstructed.

For authorship guidance and composition rules, see [ART-COMPOSITION](../docs/ART-COMPOSITION.md). Run the [asset contract validator](../docs/ASSET-VALIDATION.md) to check scene metadata, seed coverage and generate-on-miss wiring. Do not regenerate or rewrite approved archival masters as part of prompt changes.
