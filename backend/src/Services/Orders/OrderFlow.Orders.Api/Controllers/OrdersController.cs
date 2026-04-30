using MediatR;
using Microsoft.AspNetCore.Mvc;
using OrderFlow.Orders.Application.Common;
using OrderFlow.SharedKernel.Common;
using OrderFlow.Orders.Application.Orders.Commands.AddOrderItem;
using OrderFlow.Orders.Application.Orders.Commands.CancelOrder;
using OrderFlow.Orders.Application.Orders.Commands.ConfirmOrder;
using OrderFlow.Orders.Application.Orders.Commands.CreateOrder;
using OrderFlow.Orders.Application.Orders.Queries.GetOrderById;
using OrderFlow.Orders.Application.Orders.Queries.GetOrdersByCustomer;

namespace OrderFlow.Orders.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class OrdersController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(CreateOrderCommand command, CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value }, result.Value)
            : BadRequest(result.Error);
    }

    [HttpPost("{id:guid}/items")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddItem(Guid id, AddOrderItemCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (id != command.OrderId)
            return BadRequest(new Error("Route.Mismatch", "Route ID does not match command OrderId."));

        var result = await mediator.Send(command, ct);

        return result.IsSuccess ? NoContent() : HandleFailure(result);
    }

    [HttpPost("{id:guid}/confirm")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Confirm(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new ConfirmOrderCommand(id), ct);

        return result.IsSuccess ? NoContent() : HandleFailure(result);
    }

    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(Guid id, CancelOrderCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (id != command.OrderId)
            return BadRequest(new Error("Route.Mismatch", "Route ID does not match command OrderId."));

        var result = await mediator.Send(command, ct);

        return result.IsSuccess ? NoContent() : HandleFailure(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OrderDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetOrderByIdQuery(id), ct);

        return result.IsSuccess ? Ok(result.Value) : NotFound(result.Error);
    }

    [HttpGet("customer/{customerId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<OrderSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByCustomer(
        Guid customerId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetOrdersByCustomerQuery(customerId, page, pageSize), ct);
        return Ok(result.Value);
    }

    private IActionResult HandleFailure(Result result)
    {
        return result.Error.Code.Contains("NotFound", StringComparison.Ordinal)
            ? NotFound(result.Error)
            : BadRequest(result.Error);
    }
}
