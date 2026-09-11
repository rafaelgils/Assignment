using Order.Domain.Enums;
using Order.Domain.Exceptions;

namespace Order.Domain.Entities;

public class Order
{
    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public OrderStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public List<OrderItem> Items { get; private set; } = [];

    public decimal TotalAmount => Items.Sum(i => i.UnitPrice * i.Quantity);

    private Order() { }

    public static Order Create(Guid id, Guid customerId, IEnumerable<(string ProductName, int Quantity, decimal UnitPrice)> items)
    {
        var order = new Order
        {
            Id = id,
            CustomerId = customerId,
            Status = OrderStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        foreach (var item in items)
        {
            order.Items.Add(new OrderItem(order.Id, item.ProductName, item.Quantity, item.UnitPrice));
        }

        if (order.Items.Count == 0)
            throw new DomainValidationException("An order must have at least one item.");

        return order;
    }

    public void Cancel()
    {
        if (Status != OrderStatus.Pending)
            throw new InvalidOrderStateException("Only orders with status Pending can be cancelled.");

        Status = OrderStatus.Cancelled;
    }
}
