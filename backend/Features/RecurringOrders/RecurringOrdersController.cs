using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HiveOrders.Api.Features.RecurringOrders;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/recurring-orders")]
[Authorize]
public class RecurringOrdersController : ControllerBase
{
    private readonly IRecurringOrderHandler _handler;

    public RecurringOrdersController(IRecurringOrderHandler handler)
    {
        _handler = handler;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException();

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RecurringOrderTemplateResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RecurringOrderTemplateResponse>>> GetMyTemplates(CancellationToken cancellationToken)
    {
        var templates = await _handler.GetMyTemplatesAsync(UserId, cancellationToken);
        return Ok(templates);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(RecurringOrderTemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RecurringOrderTemplateResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        var template = await _handler.GetByIdAsync(id, UserId, cancellationToken);
        if (template == null) return NotFound();
        return Ok(template);
    }

    [HttpPost]
    [ProducesResponseType(typeof(RecurringOrderTemplateResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<RecurringOrderTemplateResponse>> Create(
        [FromBody] CreateRecurringOrderTemplateRequest request,
        CancellationToken cancellationToken)
    {
        var template = await _handler.CreateAsync(request, UserId, cancellationToken);
        if (template == null) return BadRequest();
        return CreatedAtAction(nameof(GetById), new { id = template.Id }, template);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(RecurringOrderTemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RecurringOrderTemplateResponse>> Update(
        int id,
        [FromBody] UpdateRecurringOrderTemplateRequest request,
        CancellationToken cancellationToken)
    {
        var template = await _handler.UpdateAsync(id, request, UserId, cancellationToken);
        if (template == null) return NotFound();
        return Ok(template);
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await _handler.DeleteAsync(id, UserId, cancellationToken);
        if (!deleted) return NotFound();
        return NoContent();
    }
}
