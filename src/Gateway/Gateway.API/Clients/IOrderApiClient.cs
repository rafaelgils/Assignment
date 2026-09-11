namespace Gateway.API.Clients;

public enum CancelOrderResult
{
    Cancelled,
    NotFound,
    Conflict
}

public interface IOrderApiClient
{
    Task<PagedOrdersModel> GetOrdersAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task<OrderModel?> GetOrderByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CancelOrderResult> CancelOrderAsync(Guid id, CancellationToken cancellationToken = default);
}
