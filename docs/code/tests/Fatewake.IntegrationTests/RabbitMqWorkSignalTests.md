# RabbitMqWorkSignalTests

Set `FATEWAKE_TEST_RABBITMQ` to a disposable broker's AMQP connection string to exercise real publish/consume wakeups. The test creates uniquely named queues and exchanges and deletes them after stopping its consumer. No artwork provider is invoked.
