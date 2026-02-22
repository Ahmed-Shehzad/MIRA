using System.ComponentModel.DataAnnotations;
using HiveOrders.Api.Features.OrderRounds;
using HiveOrders.Api.Shared.Identity;
using HiveOrders.Api.Shared.ValueObjects;
using PaymentStatus = HiveOrders.Api.Shared.ValueObjects.PaymentStatus;

namespace HiveOrders.Api.Features.Payments;

public class Payment
{
    public int Id { get; set; }

    public int TenantId { get; set; }

    public int OrderRoundId { get; set; }
    public OrderRound OrderRound { get; set; } = null!;

    public UserId UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public StripePaymentIntentId StripePaymentIntentId { get; set; }

    public Money Amount { get; set; }

    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
