using MediatR;
using Order.Application.Common;
using Order.Application.DTOs;
using Order.Application.Interfaces;
using Order.Application.Mapping;

namespace Order.Application.Queries.GetOrders;

public class GetOrdersQueryHandler(IOrderRepository repository) : IRequestHandler<GetOrdersQuery, PagedResult<OrderDto>>
{
    public async Task<PagedResult<OrderDto>> Handle(GetOrdersQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await repository.GetPagedAsync(request.Page, request.PageSize, cancellationToken);

        return new PagedResult<OrderDto>
        {
            Items = items.Select(o => o.ToDto()).ToList(),
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = totalCount
        };
    }
}
