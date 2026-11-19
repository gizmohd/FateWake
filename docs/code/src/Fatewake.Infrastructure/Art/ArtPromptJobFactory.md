# ArtPromptJobFactory

Coalesces authoring requests by SQL prompt identity. Pending identity state and the `art.prompt-author` step are committed together through the work builder's transaction callback; PostgreSQL polling recovers missed RabbitMQ hints.
