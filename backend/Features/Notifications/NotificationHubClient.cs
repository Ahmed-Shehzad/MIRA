using Microsoft.AspNetCore.SignalR;

namespace HiveOrders.Api.Features.Notifications;

public class NotificationHubClient : INotificationHubClient
{
    private readonly IHubContext<NotificationHub> _hubContext;

    public NotificationHubClient(IHubContext<NotificationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task SendToUserAsync(int tenantId, string userId, string type, string title, string? body, CancellationToken cancellationToken = default)
    {
        var groupName = $"user:{tenantId}:{userId}";
        return _hubContext.Clients.Group(groupName).SendAsync("Notification", new { type, title, body }, cancellationToken);
    }
}
