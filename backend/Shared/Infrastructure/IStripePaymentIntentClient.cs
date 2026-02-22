namespace HiveOrders.Api.Shared.Infrastructure;

public interface IStripePaymentIntentClient
{
    Task<StripePaymentIntentCreateResult> CreateAsync(
        long amountInCents,
        string currency,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken cancellationToken = default);
}

public record StripePaymentIntentCreateResult(string ClientSecret, string PaymentIntentId);
