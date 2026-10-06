# DurableWorkWorker

Executes durable queue work with PostgreSQL lease claims and configurable concurrency per queue. Handlers are scoped per claim; active leases are renewed by a heartbeat using a separate scoped store/DbContext so renewal never concurrently uses the handler's DbContext.

Idle queue runners await `WorkQueueWakeup` hints or the configured recovery polling interval. RabbitMQ consumption is independent of claims and execution; broker outages cannot stop database polling. `art.finalize` is enabled by default. Missing handlers and execution errors are persisted through the store's retry/failure policy and logged.
