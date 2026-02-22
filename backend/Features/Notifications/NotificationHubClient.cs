using Microsoft.AspNetCore.SignalR;

namespace HiveOrders.Api.Features.Notifications;

public class NotificationHubClient : INotificationHubClient
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly IApiGatewayWebSocketPushService _apiGatewayPush;

    public NotificationHubClient(
        IHubContext<NotificationHub> hubContext,
        IApiGatewayWebSocketPushService apiGatewayPush)
    {
        _hubContext = hubContext;
        _apiGatewayPush = apiGatewayPush;
    }

    public async Task SendToUserAsync(int tenantId, string userId, string type, string title, string? body, CancellationToken cancellationToken = default)
    {
        var payload = new { type, title, body };
        await _hubContext.Clients.Group($"user:{tenantId}:{userId}").SendAsync("Notification", payload, cancellationToken);

        await _apiGatewayPush.PushToUserAsync(tenantId, userId, type, title, body, cancellationToken);
    }
}
