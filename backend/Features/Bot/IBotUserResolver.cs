namespace HiveOrders.Api.Features.Bot;

public interface IBotUserResolver
{
    Task<string?> ResolveUserIdAsync(string externalId, CancellationToken cancellationToken = default);
}
