using System.ComponentModel.DataAnnotations;

namespace HiveOrders.Api.Features.Notifications;

public class Notification
{
    public int Id { get; set; }

    public int TenantId { get; set; }

    public required string UserId { get; set; }

    [Required]
    [MaxLength(50)]
    public required string Type { get; set; }

    [Required]
    [MaxLength(200)]
    public required string Title { get; set; }

    [MaxLength(1000)]
    public string? Body { get; set; }

    public DateTime? ReadAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
