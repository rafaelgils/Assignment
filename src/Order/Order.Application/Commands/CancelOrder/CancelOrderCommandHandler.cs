using MediatR;
using Order.Application.Exceptions;
using Order.Application.Interfaces;

namespace Order.Application.Commands.CancelOrder;

public class CancelOrderCommandHandler(IOrderRepository repository) : IRequestHandler<CancelOrderCommand>
{
    public async Task Handle(CancelOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await repository.GetByIdAsync(request.OrderId, cancellationToken)
            ?? throw new NotFoundException($"Order '{request.OrderId}' was not found.");

        order.Cancel();

        await repository.SaveChangesAsync(cancellationToken);
    }
}
