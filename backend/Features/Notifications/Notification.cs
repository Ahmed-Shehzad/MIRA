using System.ComponentModel.DataAnnotations;
using HiveOrders.Api.Shared.ValueObjects;

namespace HiveOrders.Api.Features.Notifications;

public class Notification
{
    public int Id { get; set; }

    public int TenantId { get; set; }

    public UserId UserId { get; set; }

    public NotificationType Type { get; set; }

    [Required]
    [MaxLength(200)]
    public required string Title { get; set; }

    [MaxLength(1000)]
    public string? Body { get; set; }

    public DateTime? ReadAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
