using OrderEntity = Order.Domain.Entities.Order;

namespace Order.Application.Interfaces;

public interface IOrderRepository
{
    Task<bool> ExistsAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<OrderEntity?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<OrderEntity> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task AddAsync(OrderEntity order, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
