# ArtPromptSeedImporter

Imports `art/prompt-seeds/day1-v1.json` only when a matching SQL prompt identity is absent. It never consults prompt Markdown during requests or replaces existing revisions. The seed JSON is a versioned historical artifact, not the live prompt store.
