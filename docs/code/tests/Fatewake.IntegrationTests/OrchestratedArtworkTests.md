# OrchestratedArtworkTests

This opt-in end-to-end test targets a running AppHost rather than starting an in-process substitute worker. Set `FATEWAKE_ORCHESTRATION_CONNECTION` to its database, `FATEWAKE_ORCHESTRATION_ART_ROOT` to the shared storage directory, and `FATEWAKE_TEST_RABBITMQ` to its AMQP connection.

It seeds uniquely named approved artwork, enqueues the seven-step reuse workflow via RabbitMQ, and verifies the real worker finishes finalization without any provider invocation. Only the test's own job, artwork records, and uniquely named binary directory are removed afterward. It never deletes or recreates the target database.
