using Refit;

namespace HiveOrders.Api.Shared.HttpClients;

/// <summary>
/// Sample Refit interface for outgoing HTTP calls to external services.
/// Replace or extend with concrete APIs (e.g. SendGrid, Stripe, external order systems).
/// </summary>
public interface IExternalServiceApi
{
    [Get("/health")]
    Task<ApiResponse<string>> GetHealthAsync(CancellationToken cancellationToken = default);
}
