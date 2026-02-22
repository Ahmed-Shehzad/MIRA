namespace HiveOrders.Api.Features.Notifications;

public class NotificationHubClient : INotificationHubClient
{
    private readonly IApiGatewayWebSocketPushService _apiGatewayPush;

    public NotificationHubClient(IApiGatewayWebSocketPushService apiGatewayPush)
    {
        _apiGatewayPush = apiGatewayPush;
    }

    public Task SendToUserAsync(int tenantId, string userId, string type, string title, string? body, CancellationToken cancellationToken = default)
    {
        return _apiGatewayPush.PushToUserAsync(tenantId, userId, type, title, body, cancellationToken);
    }
}
