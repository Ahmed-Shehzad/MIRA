namespace HiveOrders.Api.Features.Payments;

public interface IPaymentService
{
    Task<PaymentIntentResult> CreatePaymentIntentAsync(int orderRoundId, decimal amount, string userId, CancellationToken cancellationToken = default);
    Task<bool> HandlePaymentIntentSucceededAsync(string paymentIntentId, CancellationToken cancellationToken = default);
}

public record PaymentIntentResult(
    [property: System.Text.Json.Serialization.JsonPropertyName("clientSecret")] string ClientSecret,
    [property: System.Text.Json.Serialization.JsonPropertyName("paymentIntentId")] string PaymentIntentId);
