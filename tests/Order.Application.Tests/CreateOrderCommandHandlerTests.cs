using Moq;
using Order.Application.Commands.CreateOrder;
using Order.Application.Interfaces;
using Order.Domain.Exceptions;
using OrderEntity = Order.Domain.Entities.Order;

namespace Order.Application.Tests;

public class CreateOrderCommandHandlerTests
{
    private static CreateOrderCommand ValidCommand(Guid orderId) => new(
        orderId,
        Guid.NewGuid(),
        [new CreateOrderItemInput("Widget", 2, 9.99m)]);

    [Fact]
    public async Task Creates_order_when_it_does_not_exist_yet()
    {
        var repository = new Mock<IOrderRepository>();
        repository.Setup(r => r.ExistsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var handler = new CreateOrderCommandHandler(repository.Object);
        var command = ValidCommand(Guid.NewGuid());

        await handler.Handle(command, CancellationToken.None);

        repository.Verify(r => r.AddAsync(It.IsAny<OrderEntity>(), It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Does_not_create_duplicate_order_for_same_order_id()
    {
        var repository = new Mock<IOrderRepository>();
        repository.Setup(r => r.ExistsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var handler = new CreateOrderCommandHandler(repository.Object);
        var command = ValidCommand(Guid.NewGuid());

        await handler.Handle(command, CancellationToken.None);

        repository.Verify(r => r.AddAsync(It.IsAny<OrderEntity>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Throws_when_order_has_no_items()
    {
        var repository = new Mock<IOrderRepository>();
        repository.Setup(r => r.ExistsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var handler = new CreateOrderCommandHandler(repository.Object);
        var command = new CreateOrderCommand(Guid.NewGuid(), Guid.NewGuid(), []);

        await Assert.ThrowsAsync<DomainValidationException>(() => handler.Handle(command, CancellationToken.None));
    }
}
