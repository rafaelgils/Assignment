using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Order.Infrastructure.Persistence;
using OrderEntity = Order.Domain.Entities.Order;

namespace Order.API.IntegrationTests;

public class OrderRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly OrderDbContext _dbContext;
    private readonly OrderRepository _repository;

    public OrderRepositoryTests()
    {
        _connection.Open();
        var options = new DbContextOptionsBuilder<OrderDbContext>().UseSqlite(_connection).Options;
        _dbContext = new OrderDbContext(options);
        _dbContext.Database.Migrate();
        _repository = new OrderRepository(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task AddAsync_and_SaveChangesAsync_persist_the_order_with_its_items()
    {
        var order = OrderEntity.Create(Guid.NewGuid(), Guid.NewGuid(), [("Widget", 2, 9.99m)]);

        await _repository.AddAsync(order, CancellationToken.None);
        await _repository.SaveChangesAsync(CancellationToken.None);

        var stored = await _repository.GetByIdAsync(order.Id, CancellationToken.None);

        Assert.NotNull(stored);
        Assert.Equal(order.Id, stored!.Id);
        Assert.Single(stored.Items);
    }

    [Fact]
    public async Task GetByIdAsync_returns_null_for_an_unknown_id()
    {
        var stored = await _repository.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(stored);
    }

    [Fact]
    public async Task ExistsAsync_reflects_whether_the_order_was_persisted()
    {
        var order = OrderEntity.Create(Guid.NewGuid(), Guid.NewGuid(), [("Widget", 1, 5m)]);
        await _repository.AddAsync(order, CancellationToken.None);
        await _repository.SaveChangesAsync(CancellationToken.None);

        Assert.True(await _repository.ExistsAsync(order.Id, CancellationToken.None));
        Assert.False(await _repository.ExistsAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task GetPagedAsync_paginates_and_reports_the_total_count()
    {
        for (var i = 0; i < 3; i++)
        {
            var order = OrderEntity.Create(Guid.NewGuid(), Guid.NewGuid(), [("Widget", 1, 5m)]);
            await _repository.AddAsync(order, CancellationToken.None);
            await _repository.SaveChangesAsync(CancellationToken.None);
            await Task.Delay(10);
        }

        var firstPage = await _repository.GetPagedAsync(1, 2, CancellationToken.None);
        var secondPage = await _repository.GetPagedAsync(2, 2, CancellationToken.None);

        Assert.Equal(3, firstPage.TotalCount);
        Assert.Equal(2, firstPage.Items.Count);
        Assert.Equal(3, secondPage.TotalCount);
        Assert.Single(secondPage.Items);
        Assert.DoesNotContain(secondPage.Items[0].Id, firstPage.Items.Select(o => o.Id));
    }
}
