using MediatR;
using Order.Application.Interfaces;
using OrderEntity = Order.Domain.Entities.Order;

namespace Order.Application.Commands.CreateOrder;

public class CreateOrderCommandHandler(IOrderRepository repository) : IRequestHandler<CreateOrderCommand>
{
    public async Task Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        if (await repository.ExistsAsync(request.OrderId, cancellationToken))
        {
            return;
        }

        var order = OrderEntity.Create(
            request.OrderId,
            request.CustomerId,
            request.Items.Select(i => (i.ProductName, i.Quantity, i.UnitPrice)));

        await repository.AddAsync(order, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
    }
}
