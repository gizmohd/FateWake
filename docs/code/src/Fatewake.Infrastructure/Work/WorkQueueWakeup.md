# WorkQueueWakeup

Coalesces RabbitMQ availability hints into one pending wakeup per logical queue. Idle workers wait for a hint or the configured PostgreSQL polling interval, whichever comes first. Signals are not leases and may be lost or duplicated; the database remains authoritative. Shutdown cancellation interrupts waits.
