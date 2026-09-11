using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using Shared.Contracts.Messages;
using Shared.Contracts.Messaging;

namespace Gateway.API.Messaging;

public class RabbitMqOrderMessagePublisher(RabbitMqConnectionFactory connectionFactory) : IOrderMessagePublisher, IAsyncDisposable
{
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private IConnection? _connection;

    public async Task PublishCreateOrderAsync(CreateOrderMessage message, CancellationToken cancellationToken = default)
    {
        var connection = await GetConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await OrdersTopologyDeclarer.DeclareAsync(channel, cancellationToken);

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));

        await channel.BasicPublishAsync(
            exchange: OrdersTopology.ExchangeName,
            routingKey: OrdersTopology.CreateOrderRoutingKey,
            mandatory: false,
            basicProperties: new BasicProperties { Persistent = true },
            body: body,
            cancellationToken: cancellationToken);
    }

    private async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken)
    {
        if (_connection is { IsOpen: true })
        {
            return _connection;
        }

        //Para garantir a imdepotencia do cancelamento, usei um semaforo para controlar o acesso à criação da conexão.
        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (_connection is not { IsOpen: true })
            {
                _connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
            }

            return _connection;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.CloseAsync();
        }
    }
}
