using MediatR;

namespace Order.Application.Commands.CancelOrder;

public record CancelOrderCommand(Guid OrderId) : IRequest;
