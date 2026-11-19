# SceneArtCatalog

The catalog is a server-owned allowlist and context builder, not a live prompt source. Operational positive/negative prompt revisions are read from PostgreSQL; individual prompt Markdown files are never read during requests. A valid missing SQL prompt is coalesced into durable `art.prompt` work, while unknown client keys are rejected.

`SceneArt/STYLE-BIBLE.md` remains the shared versioned runtime contract and canonical character/location sheets remain continuity sources. The runtime style and exclusions sections are required, unique and nonempty. The one-time `art/prompt-seeds/day1-v1.json` import creates only missing prompt identities and does not overwrite SQL edits.

The effective image request puts subject details before enforced role, shared style, framing, and applicable canonical continuity. The immutable SQL revision and canonical visual context participate in its v5 fingerprint. The after-choice neighborhood layer additionally requires an allowlisted outcome captured from the active reached event under the survivor lock; its bounded visual description is stored with the SQL prompt identity and participates in the shared fingerprint, while Thread IDs and account/profile fields do not. Other current keys are shared authored layers and remain invariant across survivors. No player-appearance layer exists in the allowlist; appearance is never inferred from survivor profile data.

Exact approved current fingerprints are checked before image generation; an exact v4 fingerprint is considered only for a seed revision whose legacy inputs hash identically. Branch identifiers are not part of reusable visual identity. An unknown reached outcome cannot create an after-choice prompt.

Seed migration associates historic `art_generation` rows only when the legacy fingerprint, key, version, style revision and full resolved inputs prove equivalence. Historic provider submissions remain immutable; missing provenance is never synthesized. Prompt hits do not call text AI, approved image hits do not call the image provider, and prompt edits do not bulk-regenerate assets.
