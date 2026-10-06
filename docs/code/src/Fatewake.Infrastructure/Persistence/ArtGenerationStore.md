# ArtGenerationStore

PostgreSQL implementation of generation provenance persistence. Concurrent/retried creation converges on the unique WorkJobId record rather than creating duplicate invocation history.
