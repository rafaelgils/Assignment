namespace Shared.Contracts.Messaging;

/// <summary>
/// Names shared by the publisher (Gateway) and the consumer (Order) so both sides
/// agree on the exchange/queue layout without redeclaring it independently.
/// </summary>
public static class OrdersTopology
{
    public const string ExchangeName = "orders.exchange";
    public const string MainQueue = "orders.main";
    public const string RetryQueue = "orders.retry";
    public const string DeadLetterQueue = "orders.deadletter";
    public const string CreateOrderRoutingKey = "order.create";
    public const string RetryCountHeader = "x-retry-count";
    public const int MaxRetryAttempts = 3;
    public const int RetryDelayMilliseconds = 5000;
}
