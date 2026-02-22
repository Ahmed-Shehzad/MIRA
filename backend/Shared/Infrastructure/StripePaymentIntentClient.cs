using Stripe;

namespace HiveOrders.Api.Shared.Infrastructure;

public sealed class StripePaymentIntentClient : IStripePaymentIntentClient
{
    private readonly IConfiguration _configuration;

    public StripePaymentIntentClient(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<StripePaymentIntentCreateResult> CreateAsync(
        long amountInCents,
        string currency,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken cancellationToken = default)
    {
        var secretKey = _configuration["Stripe:SecretKey"] ?? throw new InvalidOperationException("Stripe:SecretKey not configured.");
        StripeConfiguration.ApiKey = secretKey;

        var options = new PaymentIntentCreateOptions
        {
            Amount = amountInCents,
            Currency = currency,
            Metadata = new Dictionary<string, string>(metadata)
        };

        var service = new PaymentIntentService();
        var intent = await service.CreateAsync(options, cancellationToken: cancellationToken);

        return new StripePaymentIntentCreateResult(intent.ClientSecret, intent.Id);
    }
}
