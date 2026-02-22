using MassTransit;

namespace HiveOrders.Api.Shared.Events;

public class PaymentCompletedConsumer : IConsumer<PaymentCompletedEvent>
{
    private readonly ILogger<PaymentCompletedConsumer> _logger;

    public PaymentCompletedConsumer(ILogger<PaymentCompletedConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<PaymentCompletedEvent> context)
    {
        var evt = context.Message;
        _logger.LogInformation(
            "Payment completed: PaymentId={PaymentId}, OrderRoundId={OrderRoundId}, Amount={Amount}",
            evt.PaymentId, evt.OrderRoundId, evt.Amount);
        return Task.CompletedTask;
    }
}
