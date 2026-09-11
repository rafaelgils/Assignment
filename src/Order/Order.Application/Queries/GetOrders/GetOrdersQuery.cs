using MediatR;
using Order.Application.Common;
using Order.Application.DTOs;

namespace Order.Application.Queries.GetOrders;

public record GetOrdersQuery(int Page, int PageSize) : IRequest<PagedResult<OrderDto>>;
