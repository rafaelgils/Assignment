using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Order.Application.Common;
using Order.Application.DTOs;
using Order.Infrastructure.Persistence;
using OrderEntity = Order.Domain.Entities.Order;

namespace Order.API.IntegrationTests;

public class OrdersEndpointTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private async Task<Guid> SeedOrderAsync()
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrderDbContext>();

        var order = OrderEntity.Create(Guid.NewGuid(), Guid.NewGuid(), [("Widget", 2, 9.99m)]);
        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();

        return order.Id;
    }

    [Fact]
    public async Task GetOrderById_returns_the_seeded_order()
    {
        var orderId = await SeedOrderAsync();
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/orders/{orderId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var order = await response.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotNull(order);
        Assert.Equal(orderId, order!.Id);
        Assert.Equal(19.98m, order.TotalAmount);
    }

    [Fact]
    public async Task CancelOrder_transitions_to_cancelled_then_conflicts_on_retry()
    {
        var orderId = await SeedOrderAsync();
        var client = factory.CreateClient();

        var firstCancel = await client.PatchAsync($"/orders/{orderId}/cancel", content: null);
        Assert.Equal(HttpStatusCode.NoContent, firstCancel.StatusCode);

        var secondCancel = await client.PatchAsync($"/orders/{orderId}/cancel", content: null);
        Assert.Equal(HttpStatusCode.Conflict, secondCancel.StatusCode);
    }

    [Fact]
    public async Task GetOrderById_returns_not_found_for_unknown_id()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/orders/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetOrders_returns_a_paginated_list()
    {
        await SeedOrderAsync();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/orders?page=1&pageSize=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedResult<OrderDto>>();
        Assert.NotNull(page);
        Assert.Single(page!.Items);
        Assert.Equal(1, page.Page);
        Assert.Equal(1, page.PageSize);
        Assert.True(page.TotalCount >= 1);
    }
}
