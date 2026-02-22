using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HiveOrders.Api.Shared.ValueObjects;

namespace HiveOrders.Api.Features.OrderRounds;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
public class OrderRoundsController : ControllerBase
{
    private readonly IOrderRoundHandler _handler;

    public OrderRoundsController(IOrderRoundHandler handler)
    {
        _handler = handler;
    }

    private UserId UserId => (UserId)(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException());

    /// <summary>Get current user's order rounds.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<OrderRoundResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OrderRoundResponse>>> GetMyOrderRounds(CancellationToken cancellationToken)
    {
        var rounds = await _handler.GetMyOrderRoundsAsync(UserId, cancellationToken);
        return Ok(rounds);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrderRoundDetailResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        var round = await _handler.GetByIdAsync((OrderRoundId)id, UserId, cancellationToken);
        if (round == null) return NotFound();
        return Ok(round);
    }

    /// <summary>Create a new order round.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(OrderRoundResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<OrderRoundResponse>> Create([FromBody] CreateOrderRoundRequest request, CancellationToken cancellationToken)
    {
        var round = await _handler.CreateAsync(request, UserId, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = round.Id }, round);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<OrderRoundResponse>> Update(int id, [FromBody] UpdateOrderRoundRequest request, CancellationToken cancellationToken)
    {
        var round = await _handler.UpdateAsync((OrderRoundId)id, request, UserId, cancellationToken);
        if (round == null) return NotFound();
        return Ok(round);
    }

    /// <summary>Add an item to an order round.</summary>
    [HttpPost("{id:int}/items")]
    [ProducesResponseType(typeof(OrderItemResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderItemResponse>> AddItem(int id, [FromBody] CreateOrderItemRequest request, CancellationToken cancellationToken)
    {
        var item = await _handler.AddItemAsync((OrderRoundId)id, request, UserId, cancellationToken);
        if (item == null)
            return BadRequest(new { message = "Cannot add item: order round not found, closed, or deadline passed." });
        return CreatedAtAction(nameof(GetById), new { id }, item);
    }

    [HttpPut("{id:int}/items/{itemId:int}")]
    public async Task<ActionResult<OrderItemResponse>> UpdateItem(int id, int itemId, [FromBody] UpdateOrderItemRequest request, CancellationToken cancellationToken)
    {
        var item = await _handler.UpdateItemAsync((OrderRoundId)id, (OrderItemId)itemId, request, UserId, cancellationToken);
        if (item == null) return NotFound();
        return Ok(item);
    }

    [HttpDelete("{id:int}/items/{itemId:int}")]
    public async Task<IActionResult> RemoveItem(int id, int itemId, CancellationToken cancellationToken)
    {
        var removed = await _handler.RemoveItemAsync((OrderRoundId)id, (OrderItemId)itemId, UserId, cancellationToken);
        if (!removed) return NotFound();
        return NoContent();
    }

    [HttpGet("{id:int}/export")]
    public async Task<ActionResult<OrderRoundDetailResponse>> Export(int id, CancellationToken cancellationToken)
    {
        var round = await _handler.GetByIdAsync((OrderRoundId)id, UserId, cancellationToken);
        if (round == null) return NotFound();
        return Ok(round);
    }
}
