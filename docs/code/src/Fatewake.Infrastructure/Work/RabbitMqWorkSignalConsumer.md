# RabbitMqWorkSignalConsumer

The worker subscribes to every queue with positive configured concurrency, including `art.finalize`. Each queue has its own channel and durable direct-exchange binding. Incoming hints wake a local worker and are acknowledged; PostgreSQL lease claims, not message delivery, determine ownership.

Connection failures are logged and retried at the recovery polling interval. PostgreSQL polling continues independently while RabbitMQ is unavailable. Consumption has no provider or binary-storage dependencies.
