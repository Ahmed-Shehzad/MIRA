using System.ComponentModel.DataAnnotations;

namespace HiveOrders.Api.Features.Notifications;

public class PushSubscription
{
    public int Id { get; set; }

    public int TenantId { get; set; }

    public required string UserId { get; set; }

    [Required]
    [MaxLength(2000)]
    public required string Endpoint { get; set; }

    [MaxLength(500)]
    public string? P256dh { get; set; }

    [MaxLength(500)]
    public string? Auth { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
