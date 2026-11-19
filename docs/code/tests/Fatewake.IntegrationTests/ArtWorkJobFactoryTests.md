# ArtWorkJobFactoryTests

Protects artwork work-job idempotency boundaries: shared requests use canonical visual fingerprints, private requests are scoped to their owning account, and a private request without an owner is rejected.
