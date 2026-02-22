namespace HiveOrders.Api.Features.Auth;

public interface IAuthService
{
    Task<AuthResponse?> GetTestTokenAsync(TestTokenRequest request, CancellationToken cancellationToken = default);

    Task<AuthResponse?> GetCurrentUserAsync(string userId, string? bearerToken, CancellationToken cancellationToken = default);
}
