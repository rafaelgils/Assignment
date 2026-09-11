using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Order.Application.Commands.CreateOrder;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.Contracts.Messages;
using Shared.Contracts.Messaging;

namespace Order.Infrastructure.Messaging;

public class CreateOrderConsumer(
    RabbitMqConnectionFactory connectionFactory,
    IServiceScopeFactory scopeFactory,
    ILogger<CreateOrderConsumer> logger) : BackgroundService
{
    private IConnection? _connection;
    private IChannel? _channel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _connection = await connectionFactory.CreateConnectionAsync(stoppingToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await OrdersTopologyDeclarer.DeclareAsync(_channel, stoppingToken);
        await _channel.BasicQosAsync(0, prefetchCount: 1, global: false, cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += OnMessageReceivedAsync;

        await _channel.BasicConsumeAsync(OrdersTopology.MainQueue, autoAck: false, consumer, stoppingToken);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // expected on shutdown
        }
    }

    private async Task OnMessageReceivedAsync(object sender, BasicDeliverEventArgs e)
    {
        var channel = _channel!;

        try
        {
            var json = Encoding.UTF8.GetString(e.Body.Span);
            var message = JsonSerializer.Deserialize<CreateOrderMessage>(json)
                ?? throw new InvalidOperationException("Could not deserialize CreateOrderMessage.");

            using var scope = scopeFactory.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            var command = new CreateOrderCommand(
                message.OrderId,
                message.CustomerId,
                message.Items.Select(i => new CreateOrderItemInput(i.ProductName, i.Quantity, i.UnitPrice)).ToList());

            await mediator.Send(command);

            await channel.BasicAckAsync(e.DeliveryTag, multiple: false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process CreateOrder message, deliveryTag={DeliveryTag}", e.DeliveryTag);
            await RouteToRetryOrDeadLetterAsync(channel, e);
        }
    }

    private static async Task RouteToRetryOrDeadLetterAsync(IChannel channel, BasicDeliverEventArgs e)
    {
        var retryCount = 0;
        if (e.BasicProperties.Headers is not null &&
            e.BasicProperties.Headers.TryGetValue(OrdersTopology.RetryCountHeader, out var raw))
        {
            retryCount = raw switch
            {
                byte[] bytes => int.Parse(Encoding.UTF8.GetString(bytes)),
                int i => i,
                _ => 0
            };
        }

        var properties = new BasicProperties
        {
            Headers = new Dictionary<string, object?>
            {
                [OrdersTopology.RetryCountHeader] = Encoding.UTF8.GetBytes((retryCount + 1).ToString())
            }
        };

        var targetQueue = retryCount + 1 < OrdersTopology.MaxRetryAttempts
            ? OrdersTopology.RetryQueue
            : OrdersTopology.DeadLetterQueue;

        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: targetQueue,
            mandatory: false,
            basicProperties: properties,
            body: e.Body);

        await channel.BasicAckAsync(e.DeliveryTag, multiple: false);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel is not null) await _channel.CloseAsync(cancellationToken);
        if (_connection is not null) await _connection.CloseAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}
