# ArtGenerationStore

PostgreSQL implementation of generation provenance persistence. Concurrent/retried creation converges on the unique WorkJobId record rather than creating duplicate invocation history.
# ArtGenerationStore

Creates or retrieves the immutable generation audit associated with a durable image-work job. It records exact resolved prompt text, prompt revision only when known, provider/model, reference and parameter metadata before provider execution; later updates capture provider results without reconstructing historic prompt provenance.
