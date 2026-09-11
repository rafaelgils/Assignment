using Moq;
using RabbitMQ.Client;
using Shared.Contracts.Messaging;

namespace Shared.Contracts.Tests;

public class OrdersTopologyDeclarerTests
{
    [Fact]
    public async Task Declares_the_exchange_and_binds_the_main_queue()
    {
        var channel = new Mock<IChannel>();

        await OrdersTopologyDeclarer.DeclareAsync(channel.Object, CancellationToken.None);

        channel.Verify(c => c.ExchangeDeclareAsync(
            OrdersTopology.ExchangeName,
            ExchangeType.Direct,
            true,
            false,
            It.IsAny<IDictionary<string, object?>>(),
            false,
            false,
            It.IsAny<CancellationToken>()), Times.Once);

        channel.Verify(c => c.QueueBindAsync(
            OrdersTopology.MainQueue,
            OrdersTopology.ExchangeName,
            OrdersTopology.CreateOrderRoutingKey,
            It.IsAny<IDictionary<string, object?>>(),
            false,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Declares_the_main_and_dead_letter_queues_without_extra_arguments()
    {
        var channel = new Mock<IChannel>();

        await OrdersTopologyDeclarer.DeclareAsync(channel.Object, CancellationToken.None);

        channel.Verify(c => c.QueueDeclareAsync(
            OrdersTopology.MainQueue, true, false, false,
            It.IsAny<IDictionary<string, object?>>(), false, false, It.IsAny<CancellationToken>()), Times.Once);

        channel.Verify(c => c.QueueDeclareAsync(
            OrdersTopology.DeadLetterQueue, true, false, false,
            It.IsAny<IDictionary<string, object?>>(), false, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Declares_the_retry_queue_with_ttl_and_dead_letter_routing_back_to_main()
    {
        var channel = new Mock<IChannel>();

        await OrdersTopologyDeclarer.DeclareAsync(channel.Object, CancellationToken.None);

        channel.Verify(c => c.QueueDeclareAsync(
            OrdersTopology.RetryQueue,
            true,
            false,
            false,
            It.Is<IDictionary<string, object?>>(args =>
                Equals(args["x-message-ttl"], OrdersTopology.RetryDelayMilliseconds) &&
                Equals(args["x-dead-letter-exchange"], string.Empty) &&
                Equals(args["x-dead-letter-routing-key"], OrdersTopology.MainQueue)),
            false,
            false,
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
