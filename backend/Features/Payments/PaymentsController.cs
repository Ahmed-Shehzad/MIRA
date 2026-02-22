using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HiveOrders.Api.Features.Payments;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/order-rounds/{orderRoundId:int}/payments")]
[Authorize]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;

    public PaymentsController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException();

    /// <summary>Create a Stripe payment intent for an order round.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(PaymentIntentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaymentIntentResult>> CreatePaymentIntent(
        int orderRoundId,
        [FromBody] CreatePaymentIntentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _paymentService.CreatePaymentIntentAsync(orderRoundId, request.Amount, UserId, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound();
        }
        catch (InvalidOperationException)
        {
            return BadRequest("Invalid request or Stripe not configured.");
        }
    }
}

public record CreatePaymentIntentRequest(
    [property: System.Text.Json.Serialization.JsonPropertyName("amount")]
    [property: System.Text.Json.Serialization.JsonRequired]
    decimal Amount);
