using MediatR;
using Order.Application.DTOs;
using Order.Application.Interfaces;
using Order.Application.Mapping;

namespace Order.Application.Queries.GetOrderById;

public class GetOrderByIdQueryHandler(IOrderRepository repository) : IRequestHandler<GetOrderByIdQuery, OrderDto?>
{
    public async Task<OrderDto?> Handle(GetOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var order = await repository.GetByIdAsync(request.Id, cancellationToken);
        return order?.ToDto();
    }
}
