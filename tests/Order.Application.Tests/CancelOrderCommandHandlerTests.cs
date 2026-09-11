using Moq;
using Order.Application.Commands.CancelOrder;
using Order.Application.Exceptions;
using Order.Application.Interfaces;
using Order.Domain.Exceptions;
using OrderEntity = Order.Domain.Entities.Order;

namespace Order.Application.Tests;

public class CancelOrderCommandHandlerTests
{
    private static OrderEntity PendingOrder() => OrderEntity.Create(
        Guid.NewGuid(),
        Guid.NewGuid(),
        [("Widget", 1, 9.99m)]);

    [Fact]
    public async Task Cancels_a_pending_order()
    {
        var order = PendingOrder();
        var repository = new Mock<IOrderRepository>();
        repository.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var handler = new CancelOrderCommandHandler(repository.Object);

        await handler.Handle(new CancelOrderCommand(order.Id), CancellationToken.None);

        Assert.Equal(Domain.Enums.OrderStatus.Cancelled, order.Status);
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Throws_when_order_is_already_cancelled()
    {
        var order = PendingOrder();
        order.Cancel();

        var repository = new Mock<IOrderRepository>();
        repository.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var handler = new CancelOrderCommandHandler(repository.Object);

        await Assert.ThrowsAsync<InvalidOrderStateException>(() =>
            handler.Handle(new CancelOrderCommand(order.Id), CancellationToken.None));
    }

    [Fact]
    public async Task Throws_not_found_when_order_does_not_exist()
    {
        var repository = new Mock<IOrderRepository>();
        repository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((OrderEntity?)null);

        var handler = new CancelOrderCommandHandler(repository.Object);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new CancelOrderCommand(Guid.NewGuid()), CancellationToken.None));
    }
}
