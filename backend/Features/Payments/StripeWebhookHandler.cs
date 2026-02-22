using Stripe;

namespace HiveOrders.Api.Features.Payments;

public class StripeWebhookHandler : IStripeWebhookHandler
{
    private readonly IPaymentService _paymentService;
    private readonly IConfiguration _configuration;

    public StripeWebhookHandler(IPaymentService paymentService, IConfiguration configuration)
    {
        _paymentService = paymentService;
        _configuration = configuration;
    }

    public async Task<StripeWebhookResult> HandleAsync(string json, string stripeSignature, CancellationToken cancellationToken = default)
    {
        var webhookSecret = _configuration["Stripe:WebhookSecret"];
        if (string.IsNullOrEmpty(webhookSecret))
            return new StripeWebhookResult(false, 400, "Stripe webhook not configured.");

        Stripe.Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(json, stripeSignature, webhookSecret);
        }
        catch (StripeException)
        {
            return new StripeWebhookResult(false, 400, "Invalid signature.");
        }

        if (stripeEvent.Type != "payment_intent.succeeded")
            return new StripeWebhookResult(true, 200);

        var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
        if (paymentIntent == null)
            return new StripeWebhookResult(true, 200);

        var updated = await _paymentService.HandlePaymentIntentSucceededAsync(paymentIntent.Id, cancellationToken);
        return updated ? new StripeWebhookResult(true, 200) : new StripeWebhookResult(false, 500, "Payment not found.");
    }
}
