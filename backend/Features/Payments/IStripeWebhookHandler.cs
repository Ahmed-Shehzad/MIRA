namespace HiveOrders.Api.Features.Payments;

public interface IStripeWebhookHandler
{
    Task<StripeWebhookResult> HandleAsync(string json, string stripeSignature, CancellationToken cancellationToken = default);
}
