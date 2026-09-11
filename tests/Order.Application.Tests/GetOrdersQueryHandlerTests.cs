using Moq;
using Order.Application.Interfaces;
using Order.Application.Queries.GetOrders;
using OrderEntity = Order.Domain.Entities.Order;

namespace Order.Application.Tests;

public class GetOrdersQueryHandlerTests
{
    [Fact]
    public async Task Maps_paged_orders_to_dtos()
    {
        var order = OrderEntity.Create(Guid.NewGuid(), Guid.NewGuid(), [("Widget", 2, 9.99m)]);
        var repository = new Mock<IOrderRepository>();
        repository.Setup(r => r.GetPagedAsync(1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<OrderEntity> { order }, 1));

        var handler = new GetOrdersQueryHandler(repository.Object);

        var result = await handler.Handle(new GetOrdersQuery(1, 10), CancellationToken.None);

        Assert.Equal(1, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal(order.Id, result.Items[0].Id);
        Assert.Equal(19.98m, result.Items[0].TotalAmount);
    }

    [Fact]
    public async Task Returns_empty_page_when_there_are_no_orders()
    {
        var repository = new Mock<IOrderRepository>();
        repository.Setup(r => r.GetPagedAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<OrderEntity>(), 0));

        var handler = new GetOrdersQueryHandler(repository.Object);

        var result = await handler.Handle(new GetOrdersQuery(2, 5), CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }
}
