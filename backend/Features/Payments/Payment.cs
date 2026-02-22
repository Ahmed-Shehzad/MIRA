using System.ComponentModel.DataAnnotations;
using HiveOrders.Api.Features.OrderRounds;
using HiveOrders.Api.Shared.Identity;

namespace HiveOrders.Api.Features.Payments;

public class Payment
{
    public int Id { get; set; }

    public int TenantId { get; set; }

    public int OrderRoundId { get; set; }
    public OrderRound OrderRound { get; set; } = null!;

    public required string UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    public required string StripePaymentIntentId { get; set; }

    [Range(0, double.MaxValue)]
    public decimal Amount { get; set; }

    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum PaymentStatus
{
    Pending,
    Completed,
    Failed
}
