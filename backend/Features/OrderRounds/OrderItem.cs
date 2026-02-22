using System.ComponentModel.DataAnnotations;
using HiveOrders.Api.Shared.Identity;

namespace HiveOrders.Api.Features.OrderRounds;

public class OrderItem
{
    public int Id { get; set; }

    public int OrderRoundId { get; set; }
    public OrderRound OrderRound { get; set; } = null!;

    public required string UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    [Required]
    [MaxLength(500)]
    public required string Description { get; set; }

    [Range(0, double.MaxValue)]
    public decimal Price { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}
