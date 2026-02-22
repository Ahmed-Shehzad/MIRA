using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HiveOrders.Api.Shared.Infrastructure;
using Stripe;

namespace HiveOrders.Api.Features.Payments;

[ApiController]
[Route("api/v1/webhooks/stripe")]
[AllowAnonymous]
public class StripeWebhookController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly IConfiguration _configuration;

    public StripeWebhookController(IPaymentService paymentService, IConfiguration configuration)
    {
        _paymentService = paymentService;
        _configuration = configuration;
    }

    [HttpPost]
    [Consumes("application/json")]
    public async Task<IActionResult> HandleWebhook(
        [RawBody] string json,
        [FromHeader(Name = "Stripe-Signature")] string stripeSignature,
        CancellationToken cancellationToken)
    {
        var webhookSecret = _configuration["Stripe:WebhookSecret"];
        if (string.IsNullOrEmpty(webhookSecret))
            return BadRequest("Stripe webhook not configured.");

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(json, stripeSignature, webhookSecret);
        }
        catch (StripeException)
        {
            return BadRequest("Invalid signature.");
        }

        if (stripeEvent.Type != "payment_intent.succeeded")
            return Ok();

        var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
        if (paymentIntent == null)
            return Ok();

        var updated = await _paymentService.HandlePaymentIntentSucceededAsync(paymentIntent.Id, cancellationToken);
        return updated ? Ok() : StatusCode(500, "Payment not found.");
    }
}
