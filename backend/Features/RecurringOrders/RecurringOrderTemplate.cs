using System.ComponentModel.DataAnnotations;
using HiveOrders.Api.Shared.Identity;
using HiveOrders.Api.Shared.ValueObjects;

namespace HiveOrders.Api.Features.RecurringOrders;

public class RecurringOrderTemplate
{
    public int Id { get; set; }

    public int TenantId { get; set; }

    [Required]
    [MaxLength(200)]
    public required string RestaurantName { get; set; }

    [MaxLength(500)]
    public string? RestaurantUrl { get; set; }

    [Required]
    [MaxLength(50)]
    public required string CronExpression { get; set; }

    public UserId CreatedByUserId { get; set; }
    public AppUser CreatedByUser { get; set; } = null!;

    public bool IsActive { get; set; } = true;

    public DateTime? NextRunAt { get; set; }
}
