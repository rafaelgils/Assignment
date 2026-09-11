namespace Shared.Contracts.Messages;

public class CreateOrderMessage
{
    public Guid OrderId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public List<CreateOrderItemMessage> Items { get; set; } = [];
}

public class CreateOrderItemMessage
{
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}
