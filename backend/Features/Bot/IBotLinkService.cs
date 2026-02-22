namespace HiveOrders.Api.Features.Bot;

public interface IBotLinkService
{
    Task<string> CreateLinkCodeAsync(string userId, CancellationToken cancellationToken = default);
    Task<bool> ConsumeLinkCodeAsync(string code, string externalId, CancellationToken cancellationToken = default);
}
