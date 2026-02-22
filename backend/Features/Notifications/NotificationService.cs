using Microsoft.EntityFrameworkCore;
using HiveOrders.Api.Shared.Data;
using HiveOrders.Api.Shared.Infrastructure;
using HiveOrders.Api.Shared.ValueObjects;

namespace HiveOrders.Api.Features.Notifications;

public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _configuration;

    public NotificationService(ApplicationDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    public string? GetVapidPublicKey() => _configuration["Push:VapidPublicKey"];

    public async Task<IReadOnlyList<NotificationResponse>> GetUnreadAsync(UserId userId, TenantId tenantId, CancellationToken cancellationToken = default)
    {
        return await _db.Set<Notification>()
            .Where(n => n.UserId == userId && n.TenantId == tenantId && n.ReadAt == null)
            .OrderByDescending(n => n.CreatedAt)
            .Take(50)
            .Select(n => new NotificationResponse(n.Id, n.Type.Value, n.Title, n.Body ?? "", n.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> MarkReadAsync(int id, UserId userId, TenantId tenantId, CancellationToken cancellationToken = default)
    {
        var notification = await _db.Set<Notification>()
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId && n.TenantId == tenantId, cancellationToken);

        if (notification == null)
            return false;

        notification.ReadAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task SubscribePushAsync(PushSubscribeRequest request, UserId userId, TenantId tenantId, CancellationToken cancellationToken = default)
    {
        var endpoint = (PushEndpoint)request.Endpoint;
        var existing = await _db.Set<PushSubscription>()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.UserId == userId && s.Endpoint == endpoint, cancellationToken);
        if (existing != null)
            return;

        _db.Set<PushSubscription>().Add(new PushSubscription
        {
            TenantId = tenantId,
            UserId = userId,
            Endpoint = endpoint,
            P256dh = request.Keys?.P256dh,
            Auth = request.Keys?.Auth
        });
        await _db.SaveChangesAsync(cancellationToken);
    }
}
