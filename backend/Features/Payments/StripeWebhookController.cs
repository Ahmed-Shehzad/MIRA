using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HiveOrders.Api.Shared.Infrastructure;

namespace HiveOrders.Api.Features.Payments;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/webhooks/stripe")]
[AllowAnonymous]
public class StripeWebhookController : ControllerBase
{
    private readonly IStripeWebhookHandler _webhookHandler;

    public StripeWebhookController(IStripeWebhookHandler webhookHandler)
    {
        _webhookHandler = webhookHandler;
    }

    [HttpPost]
    [Consumes("application/json")]
    public async Task<IActionResult> HandleWebhook(
        [RawBody] string json,
        [FromHeader(Name = "Stripe-Signature")] string stripeSignature,
        CancellationToken cancellationToken)
    {
        var result = await _webhookHandler.HandleAsync(json, stripeSignature, cancellationToken);
        return result.Success ? Ok() : StatusCode(result.StatusCode, result.Message);
    }
}
