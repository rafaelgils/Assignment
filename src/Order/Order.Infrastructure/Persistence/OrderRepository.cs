using Microsoft.EntityFrameworkCore;
using Order.Application.Interfaces;
using OrderEntity = Order.Domain.Entities.Order;

namespace Order.Infrastructure.Persistence;

public class OrderRepository(OrderDbContext dbContext) : IOrderRepository
{
    public Task<bool> ExistsAsync(Guid orderId, CancellationToken cancellationToken = default) =>
        dbContext.Orders.AnyAsync(o => o.Id == orderId, cancellationToken);

    public Task<OrderEntity?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken = default) =>
        dbContext.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

    public async Task<(IReadOnlyList<OrderEntity> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Orders.Include(o => o.Items).OrderByDescending(o => o.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task AddAsync(OrderEntity order, CancellationToken cancellationToken = default) =>
        await dbContext.Orders.AddAsync(order, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
