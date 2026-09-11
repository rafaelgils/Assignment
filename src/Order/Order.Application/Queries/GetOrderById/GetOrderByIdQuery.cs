using MediatR;
using Order.Application.DTOs;

namespace Order.Application.Queries.GetOrderById;

public record GetOrderByIdQuery(Guid Id) : IRequest<OrderDto?>;
