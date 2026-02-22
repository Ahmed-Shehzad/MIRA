using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WebPush;
using HiveOrders.Api.Shared.Data;
using HiveOrders.Api.Shared.ValueObjects;
using PushSubscriptionEntity = HiveOrders.Api.Features.Notifications.PushSubscription;

namespace HiveOrders.Api.Features.Notifications;

public class PushNotificationService : IPushNotificationService
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _configuration;

    public PushNotificationService(ApplicationDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    public async Task SendToUserAsync(int tenantId, string userId, string title, string? body, CancellationToken cancellationToken = default)
    {
        var vapidPublic = _configuration["Push:VapidPublicKey"];
        var vapidPrivate = _configuration["Push:VapidPrivateKey"];
        if (string.IsNullOrEmpty(vapidPublic) || string.IsNullOrEmpty(vapidPrivate))
            return;

        var tid = (TenantId)tenantId;
        var uid = (UserId)userId;
        var subscriptions = await _db.Set<PushSubscriptionEntity>()
            .Where(s => s.TenantId == tid && s.UserId == uid)
            .ToListAsync(cancellationToken);

        if (subscriptions.Count == 0)
            return;

        var vapidDetails = new VapidDetails("mailto:noreply@hive.local", vapidPublic, vapidPrivate);
        var client = new WebPushClient();
        var payload = JsonSerializer.Serialize(new { title, body = body ?? "" });
        var toRemove = new List<PushSubscriptionEntity>();

        foreach (var sub in subscriptions)
        {
            try
            {
                var subscription = new WebPush.PushSubscription(sub.Endpoint.Value, sub.P256dh ?? "", sub.Auth ?? "");
                await client.SendNotificationAsync(subscription, payload, vapidDetails, cancellationToken);
            }
            catch (WebPushException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Gone || ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                toRemove.Add(sub);
            }
        }

        foreach (var sub in toRemove)
            _db.Set<PushSubscriptionEntity>().Remove(sub);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
