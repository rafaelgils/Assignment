using Order.Application.DTOs;
using OrderEntity = Order.Domain.Entities.Order;

namespace Order.Application.Mapping;

public static class OrderMappingExtensions
{
    public static OrderDto ToDto(this OrderEntity order) => new()
    {
        Id = order.Id,
        CustomerId = order.CustomerId,
        Status = order.Status.ToString(),
        CreatedAt = order.CreatedAt,
        TotalAmount = order.TotalAmount,
        Items = order.Items.Select(i => new OrderItemDto
        {
            Id = i.Id,
            ProductName = i.ProductName,
            Quantity = i.Quantity,
            UnitPrice = i.UnitPrice
        }).ToList()
    };
}
