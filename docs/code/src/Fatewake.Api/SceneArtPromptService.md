# SceneArtPromptService

Builds SQL prompt identities from server-owned layer metadata and queues one durable `art.prompt` authoring job on a valid miss. Pending state and work persist atomically; reached outcome context is captured before queuing and affects only the outcome-dependent layer fingerprint. Current shared prompts do not include profile, account, or Thread identifiers.
