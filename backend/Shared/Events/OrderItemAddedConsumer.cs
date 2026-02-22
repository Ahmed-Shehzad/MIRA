using MassTransit;

namespace HiveOrders.Api.Shared.Events;

public class OrderItemAddedConsumer : IConsumer<OrderItemAddedEvent>
{
    private readonly ILogger<OrderItemAddedConsumer> _logger;

    public OrderItemAddedConsumer(ILogger<OrderItemAddedConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<OrderItemAddedEvent> context)
    {
        var evt = context.Message;
        _logger.LogInformation(
            "Order item added: OrderRoundId={OrderRoundId}, ItemId={ItemId}, UserId={UserId}",
            evt.OrderRoundId, evt.OrderItemId, evt.UserId);
        return Task.CompletedTask;
    }
}
