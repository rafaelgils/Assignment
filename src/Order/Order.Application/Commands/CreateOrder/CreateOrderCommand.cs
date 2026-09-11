using MediatR;

namespace Order.Application.Commands.CreateOrder;

public record CreateOrderItemInput(string ProductName, int Quantity, decimal UnitPrice);

public record CreateOrderCommand(Guid OrderId, Guid CustomerId, List<CreateOrderItemInput> Items) : IRequest;
