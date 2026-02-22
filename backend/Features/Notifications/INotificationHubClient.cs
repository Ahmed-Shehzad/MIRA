namespace HiveOrders.Api.Features.Notifications;

public interface INotificationHubClient
{
    Task SendToUserAsync(int tenantId, string userId, string type, string title, string? body, CancellationToken cancellationToken = default);
}
