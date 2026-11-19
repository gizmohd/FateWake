# ArtPromptAuthoringWorkStepHandler

Executes the leased `art.prompt-author` step from its immutable server-captured input. It validates provider output, creates an append-only revision, and atomically commits the prompt-head update with an `ArtWorkJobFactory` successor. Transient errors use bounded durable retries; permanent/refusal/invalid results are explicitly marked failed.
