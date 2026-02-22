using System.ComponentModel.DataAnnotations;
using HiveOrders.Api.Shared.Identity;

namespace HiveOrders.Api.Features.OrderRounds;

public class OrderRound
{
    public int Id { get; set; }

    public int TenantId { get; set; }

    [Required]
    [MaxLength(200)]
    public required string RestaurantName { get; set; }

    [MaxLength(500)]
    public string? RestaurantUrl { get; set; }

    public required string CreatedByUserId { get; set; }
    public ApplicationUser CreatedByUser { get; set; } = null!;

    public DateTime Deadline { get; set; }

    public OrderRoundStatus Status { get; set; } = OrderRoundStatus.Open;

    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}

public enum OrderRoundStatus
{
    Open,
    Closed
}
