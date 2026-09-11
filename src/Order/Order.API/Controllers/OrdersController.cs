using MediatR;
using Microsoft.AspNetCore.Mvc;
using Order.Application.Commands.CancelOrder;
using Order.Application.DTOs;
using Order.Application.Queries.GetOrderById;
using Order.Application.Queries.GetOrders;

namespace Order.API.Controllers;

[ApiController]
[Route("orders")]
public class OrdersController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetOrders([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var result = await mediator.Send(new GetOrdersQuery(page, pageSize));
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderDto>> GetById(Guid id)
    {
        var order = await mediator.Send(new GetOrderByIdQuery(id));
        return order is null ? NotFound() : Ok(order);
    }

    [HttpPatch("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        await mediator.Send(new CancelOrderCommand(id));
        return NoContent();
    }
}
