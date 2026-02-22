using MassTransit;

namespace HiveOrders.Api.Shared.Events;

public class OrderRoundClosedConsumer : IConsumer<OrderRoundClosedEvent>
{
    private readonly ILogger<OrderRoundClosedConsumer> _logger;

    public OrderRoundClosedConsumer(ILogger<OrderRoundClosedConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<OrderRoundClosedEvent> context)
    {
        var evt = context.Message;
        _logger.LogInformation(
            "Order round closed: OrderRoundId={OrderRoundId}, CreatedByUserId={CreatedByUserId}",
            evt.OrderRoundId, evt.CreatedByUserId);
        return Task.CompletedTask;
    }
}
