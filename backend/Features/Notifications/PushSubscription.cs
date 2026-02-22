using System.ComponentModel.DataAnnotations;
using HiveOrders.Api.Shared.ValueObjects;

namespace HiveOrders.Api.Features.Notifications;

public class PushSubscription
{
    public int Id { get; set; }

    public int TenantId { get; set; }

    public UserId UserId { get; set; }

    public PushEndpoint Endpoint { get; set; }

    [MaxLength(500)]
    public string? P256dh { get; set; }

    [MaxLength(500)]
    public string? Auth { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
