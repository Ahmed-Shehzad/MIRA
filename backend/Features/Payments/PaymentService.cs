using Microsoft.EntityFrameworkCore;
using HiveOrders.Api.Shared.Data;
using HiveOrders.Api.Shared.Events;
using HiveOrders.Api.Shared.Infrastructure;
using MassTransit;
using Stripe;

namespace HiveOrders.Api.Features.Payments;

public class PaymentService : IPaymentService
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ITenantContext _tenantContext;

    public PaymentService(ApplicationDbContext db, IConfiguration configuration, IPublishEndpoint publishEndpoint, ITenantContext tenantContext)
    {
        _db = db;
        _configuration = configuration;
        _publishEndpoint = publishEndpoint;
        _tenantContext = tenantContext;
    }

    public async Task<PaymentIntentResult> CreatePaymentIntentAsync(int orderRoundId, decimal amount, string userId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.TenantId ?? throw new UnauthorizedAccessException("Tenant context required.");
        var round = await _db.OrderRounds.FirstOrDefaultAsync(o => o.Id == orderRoundId && o.TenantId == tenantId, cancellationToken);
        if (round == null || round.CreatedByUserId != userId)
            throw new InvalidOperationException("Order round not found or access denied.");

        var secretKey = _configuration["Stripe:SecretKey"] ?? throw new InvalidOperationException("Stripe:SecretKey not configured.");
        StripeConfiguration.ApiKey = secretKey;

        var amountInCents = (long)(amount * 100);
        var options = new PaymentIntentCreateOptions
        {
            Amount = amountInCents,
            Currency = "usd",
            Metadata = new Dictionary<string, string>
            {
                ["OrderRoundId"] = orderRoundId.ToString(),
                ["UserId"] = userId
            }
        };

        var service = new PaymentIntentService();
        var intent = await service.CreateAsync(options, cancellationToken: cancellationToken);

        var payment = new Payment
        {
            TenantId = tenantId,
            OrderRoundId = orderRoundId,
            UserId = userId,
            StripePaymentIntentId = intent.Id,
            Amount = amount,
            Status = PaymentStatus.Pending
        };

        _db.Payments.Add(payment);
        await _db.SaveChangesAsync(cancellationToken);

        return new PaymentIntentResult(intent.ClientSecret, intent.Id);
    }

    public async Task<bool> HandlePaymentIntentSucceededAsync(string paymentIntentId, CancellationToken cancellationToken = default)
    {
        var payment = await _db.Payments
            .FirstOrDefaultAsync(p => p.StripePaymentIntentId == paymentIntentId, cancellationToken);

        if (payment == null)
            return false;

        payment.Status = PaymentStatus.Completed;
        await _db.SaveChangesAsync(cancellationToken);

        await _publishEndpoint.Publish(new PaymentCompletedEvent(
            payment.Id, payment.OrderRoundId, payment.UserId, payment.Amount), cancellationToken);

        return true;
    }
}
