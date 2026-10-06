using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Fatewake.Infrastructure.Work;

/// <summary>Publishes durable-work wakeup hints without requiring RabbitMQ for database polling.</summary>
/// <see href="../../../docs/code/src/Fatewake.Infrastructure/Work/RabbitMqWorkSignalBus.md">RabbitMqWorkSignalBus documentation</see>
public sealed class RabbitMqWorkSignalBus(IOptions<RabbitMqWorkOptions> options) : IWorkSignalBus, IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IConnection? _connection;
    private IChannel? _channel;

    /// <inheritdoc />
    public async Task SignalAsync(string queue, Guid stepId, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (_channel is null || !_channel.IsOpen)
            {
                await CloseAsync();
                var factory = new ConnectionFactory { Uri = new Uri(options.Value.ConnectionString) };
                _connection = await factory.CreateConnectionAsync(ct);
                _channel = await _connection.CreateChannelAsync(cancellationToken: ct);
                await _channel.ExchangeDeclareAsync(options.Value.Exchange, ExchangeType.Direct,
                    durable: true, autoDelete: false, cancellationToken: ct);
            }

            await _channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
            await _channel.QueueBindAsync(queue, options.Value.Exchange, queue, cancellationToken: ct);
            var body = JsonSerializer.SerializeToUtf8Bytes(new { stepId });
            var props = new BasicProperties { Persistent = true, MessageId = stepId.ToString("N"), ContentType = "application/json" };
            await _channel.BasicPublishAsync(options.Value.Exchange, queue, false, props, body, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Closes the publisher after all in-flight publications have finished.</summary>
    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync();
        try { await CloseAsync(); }
        finally { _gate.Release(); }
        _gate.Dispose();
    }

    private async Task CloseAsync()
    {
        if (_channel is not null) await _channel.DisposeAsync();
        if (_connection is not null) await _connection.DisposeAsync();
        _channel = null;
        _connection = null;
    }
}
