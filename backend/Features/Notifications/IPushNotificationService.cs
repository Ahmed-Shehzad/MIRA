namespace HiveOrders.Api.Features.Notifications;

public interface IPushNotificationService
{
    Task SendToUserAsync(int tenantId, string userId, string title, string? body, CancellationToken cancellationToken = default);
}
