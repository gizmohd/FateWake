using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Fatewake.Infrastructure.Work;

/// <summary>Consumes availability hints to wake queue workers; PostgreSQL still owns claims and retries.</summary>
/// <see href="../../../docs/code/src/Fatewake.Infrastructure/Work/RabbitMqWorkSignalConsumer.md">RabbitMqWorkSignalConsumer documentation</see>
public sealed class RabbitMqWorkSignalConsumer(
    IOptions<RabbitMqWorkOptions> rabbit,
    IOptions<WorkExecutionOptions> work,
    WorkQueueWakeup wakeup,
    ILogger<RabbitMqWorkSignalConsumer> log) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var factory = new ConnectionFactory
                {
                    Uri = new Uri(rabbit.Value.ConnectionString),
                    AutomaticRecoveryEnabled = false
                };
                await using var connection = await factory.CreateConnectionAsync(stoppingToken);
                var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                connection.ConnectionShutdownAsync += (_, _) =>
                {
                    closed.TrySetResult();
                    return Task.CompletedTask;
                };
                var channels = new List<IChannel>();
                try
                {
                    foreach (var queue in work.Value.QueueConcurrency.Where(x => x.Value > 0).Select(x => x.Key))
                    {
                        var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
                        channels.Add(channel);
                        await channel.ExchangeDeclareAsync(rabbit.Value.Exchange, ExchangeType.Direct,
                            durable: true, autoDelete: false, cancellationToken: stoppingToken);
                        await channel.QueueDeclareAsync(queue, durable: true, exclusive: false,
                            autoDelete: false, cancellationToken: stoppingToken);
                        await channel.QueueBindAsync(queue, rabbit.Value.Exchange, queue, cancellationToken: stoppingToken);
                        await channel.BasicQosAsync(0, 1, false, stoppingToken);
                        var consumer = new AsyncEventingBasicConsumer(channel);
                        consumer.ReceivedAsync += async (_, message) =>
                        {
                            wakeup.Signal(queue);
                            await channel.BasicAckAsync(message.DeliveryTag, false, stoppingToken);
                        };
                        await channel.BasicConsumeAsync(queue, false, consumer, stoppingToken);
                    }

                    log.LogInformation("RabbitMQ work signal consumers are ready for {QueueCount} queues", channels.Count);
                    await closed.Task.WaitAsync(stoppingToken);
                    throw new IOException("RabbitMQ work signal connection closed.");
                }
                finally
                {
                    foreach (var channel in channels) await channel.DisposeAsync();
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                log.LogWarning(ex, "RabbitMQ wakeup consumption unavailable; PostgreSQL polling remains active");
                try { await Task.Delay(work.Value.PollInterval, stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            }
        }
    }
}
