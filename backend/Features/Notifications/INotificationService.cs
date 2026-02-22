using HiveOrders.Api.Shared.ValueObjects;

namespace HiveOrders.Api.Features.Notifications;

public interface INotificationService
{
    string? GetVapidPublicKey();

    Task<IReadOnlyList<NotificationResponse>> GetUnreadAsync(UserId userId, TenantId tenantId, CancellationToken cancellationToken = default);

    Task<bool> MarkReadAsync(int id, UserId userId, TenantId tenantId, CancellationToken cancellationToken = default);

    Task SubscribePushAsync(PushSubscribeRequest request, UserId userId, TenantId tenantId, CancellationToken cancellationToken = default);
}
