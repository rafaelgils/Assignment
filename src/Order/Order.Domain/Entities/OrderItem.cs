using Order.Domain.Exceptions;

namespace Order.Domain.Entities;

public class OrderItem
{
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public string ProductName { get; private set; } = string.Empty;
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }

    private OrderItem() { }

    internal OrderItem(Guid orderId, string productName, int quantity, decimal unitPrice)
    {
        if (quantity <= 0)
            throw new DomainValidationException("A quantidade deve ser maior que zero.");

        if (unitPrice <= 0)
            throw new DomainValidationException("O preço unitário deve ser maior que zero.");

        Id = Guid.NewGuid();
        OrderId = orderId;
        ProductName = productName;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }
}
