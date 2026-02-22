using System.ComponentModel.DataAnnotations;
using HiveOrders.Api.Shared.Identity;
using HiveOrders.Api.Shared.ValueObjects;

namespace HiveOrders.Api.Features.OrderRounds;

public class OrderItem
{
    public int Id { get; set; }

    public int OrderRoundId { get; set; }
    public OrderRound OrderRound { get; set; } = null!;

    public UserId UserId { get; set; }
    public AppUser User { get; set; } = null!;

    [Required]
    [MaxLength(500)]
    public required string Description { get; set; }

    public Money Price { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}
