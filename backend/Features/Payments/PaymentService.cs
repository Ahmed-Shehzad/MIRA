using Microsoft.EntityFrameworkCore;
using HiveOrders.Api.Shared.Data;
using HiveOrders.Api.Shared.Events;
using HiveOrders.Api.Shared.Infrastructure;
using HiveOrders.Api.Shared.ValueObjects;
using MassTransit;

namespace HiveOrders.Api.Features.Payments;

public class PaymentService : IPaymentService
{
    private readonly ApplicationDbContext _db;
    private readonly IStripePaymentIntentClient _stripeClient;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ITenantContext _tenantContext;

    public PaymentService(
        ApplicationDbContext db,
        IStripePaymentIntentClient stripeClient,
        IPublishEndpoint publishEndpoint,
        ITenantContext tenantContext)
    {
        _db = db;
        _stripeClient = stripeClient;
        _publishEndpoint = publishEndpoint;
        _tenantContext = tenantContext;
    }

    public async Task<PaymentIntentResult> CreatePaymentIntentAsync(int orderRoundId, decimal amount, string userId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.TenantId ?? throw new UnauthorizedAccessException("Tenant context required.");
        var roundId = (OrderRoundId)orderRoundId;
        var uid = (UserId)userId;
        var round = await _db.OrderRounds.FirstOrDefaultAsync(o => o.Id == roundId && o.TenantId == tenantId.Value, cancellationToken);
        if (round == null || round.CreatedByUserId != uid)
            throw new InvalidOperationException("Order round not found or access denied.");

        var amountInCents = (long)(amount * 100);
        var metadata = new Dictionary<string, string>
        {
            ["OrderRoundId"] = orderRoundId.ToString(),
            ["UserId"] = userId
        };

        var result = await _stripeClient.CreateAsync(amountInCents, "usd", metadata, cancellationToken);

        var payment = new Payment
        {
            TenantId = tenantId.Value,
            OrderRoundId = roundId.Value,
            UserId = uid,
            StripePaymentIntentId = (StripePaymentIntentId)result.PaymentIntentId,
            Amount = (Money)amount,
            Status = PaymentStatus.Pending
        };

        _db.Payments.Add(payment);
        await _db.SaveChangesAsync(cancellationToken);

        return new PaymentIntentResult(result.ClientSecret, result.PaymentIntentId);
    }

    public async Task<bool> HandlePaymentIntentSucceededAsync(string paymentIntentId, CancellationToken cancellationToken = default)
    {
        var pid = (StripePaymentIntentId)paymentIntentId;
        var payment = await _db.Payments
            .FirstOrDefaultAsync(p => p.StripePaymentIntentId == pid, cancellationToken);

        if (payment == null)
            return false;

        payment.Status = PaymentStatus.Completed;
        await _db.SaveChangesAsync(cancellationToken);

        await _publishEndpoint.Publish(new PaymentCompletedEvent(
            payment.Id, payment.OrderRoundId, payment.UserId.Value, payment.Amount.Value), cancellationToken);

        return true;
    }
}
