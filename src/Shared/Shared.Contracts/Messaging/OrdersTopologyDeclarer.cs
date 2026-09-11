using RabbitMQ.Client;

namespace Shared.Contracts.Messaging;

/// <summary>
/// Declares the orders exchange/queues idempotently. Called both by the Order consumer
/// (which owns the queues) and defensively by the Gateway publisher, so publishing never
/// races against the consumer's own startup declaration.
/// </summary>
public static class OrdersTopologyDeclarer
{
    public static async Task DeclareAsync(IChannel channel, CancellationToken cancellationToken = default)
    {
        await channel.ExchangeDeclareAsync(
            exchange: OrdersTopology.ExchangeName,
            type: ExchangeType.Direct,
            durable: true,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            queue: OrdersTopology.MainQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            queue: OrdersTopology.MainQueue,
            exchange: OrdersTopology.ExchangeName,
            routingKey: OrdersTopology.CreateOrderRoutingKey,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            queue: OrdersTopology.RetryQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-message-ttl"] = OrdersTopology.RetryDelayMilliseconds,
                ["x-dead-letter-exchange"] = string.Empty,
                ["x-dead-letter-routing-key"] = OrdersTopology.MainQueue
            },
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            queue: OrdersTopology.DeadLetterQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);
    }
}
