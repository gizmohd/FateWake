# ArtPromptModel

Provides artwork prompt and prompt-review EF mapping independently of `FatewakeDbContext`. The shared context calls `ArtPromptModel.Configure` from `OnModelCreating`. The handwritten additive schema is `20261119110000_SqlArtPrompts`; this repository does not maintain an EF model snapshot. Unique prompt identity and revision indexes protect idempotency and concurrent allocation. Both pending authoring and selected regeneration job references are modeled so EF orders related job/prompt inserts within the same transaction.
