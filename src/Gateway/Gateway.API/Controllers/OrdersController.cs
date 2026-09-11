using Gateway.API.Clients;
using Gateway.API.Idempotency;
using Gateway.API.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Contracts.Messages;

namespace Gateway.API.Controllers;

public record CreateOrderItemRequest(string ProductName, int Quantity, decimal UnitPrice);
public record CreateOrderRequest(Guid CustomerId, List<CreateOrderItemRequest> Items);

[ApiController]
[Route("api")]
[Authorize(Policy = "ActiveToken")]
public class OrdersController(
    IOrderMessagePublisher publisher,
    IIdempotencyStore idempotencyStore,
    IOrderApiClient orderApiClient) : ControllerBase
{

    // Cria um novo pedido de forma idempotente usando a chave de idempotência fornecida.
    // Se um pedido com a mesma chave de idempotência já existir, retorna o ID existente.
    // Caso contrário, cria um novo pedido e armazena a chave de idempotência.
    [HttpPost("order")]
    public async Task<IActionResult> CreateOrder(
        [FromBody] CreateOrderRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey)
    {
        // Verifica se a chave de idempotência foi fornecida.
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return BadRequest(new { message = "O cabeçalho 'Idempotency-Key' é obrigatório." });
        }

        var existingOrderId = await idempotencyStore.TryGetAsync(idempotencyKey);
        if (existingOrderId is not null)
        {
            return Accepted($"/api/orders/{existingOrderId}", new { id = existingOrderId });
        }

        var orderId = Guid.NewGuid();

        // Cria a mensagem de pedido a ser publicada no RabbitMQ.
        var message = new CreateOrderMessage
        {
            OrderId = orderId,
            IdempotencyKey = idempotencyKey,
            CustomerId = request.CustomerId,
            Items = request.Items.Select(i => new CreateOrderItemMessage
            {
                ProductName = i.ProductName,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice
            }).ToList()
        };

        await idempotencyStore.SaveAsync(idempotencyKey, orderId);
        await publisher.PublishCreateOrderAsync(message);

        return Accepted($"/api/orders/{orderId}", new { id = orderId });
    }

    [HttpGet("orders")]
    public async Task<IActionResult> GetOrders([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var result = await orderApiClient.GetOrdersAsync(page, pageSize);
        return Ok(result);
    }

    [HttpGet("orders/{id:guid}")]
    public async Task<IActionResult> GetOrderById(Guid id)
    {
        var order = await orderApiClient.GetOrderByIdAsync(id);
        return order is null ? NotFound() : Ok(order);
    }

    [HttpPatch("orders/{id:guid}/cancel")]
    public async Task<IActionResult> CancelOrder(Guid id)
    {
        var result = await orderApiClient.CancelOrderAsync(id);

        return result switch
        {
            CancelOrderResult.Cancelled => NoContent(),
            CancelOrderResult.NotFound => NotFound(),
            CancelOrderResult.Conflict => Conflict(new { message = "Apenas pedidos com status Pendente podem ser cancelados." }),
            _ => StatusCode(500)
        };
    }
}
