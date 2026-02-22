using MassTransit;

namespace HiveOrders.Api.Shared.Events;

public class OrderRoundCreatedConsumer : IConsumer<OrderRoundCreatedEvent>
{
    private readonly ILogger<OrderRoundCreatedConsumer> _logger;

    public OrderRoundCreatedConsumer(ILogger<OrderRoundCreatedConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<OrderRoundCreatedEvent> context)
    {
        var evt = context.Message;
        _logger.LogInformation(
            "Order round created: Id={OrderRoundId}, Restaurant={RestaurantName}, Deadline={Deadline}",
            evt.OrderRoundId, evt.RestaurantName, evt.Deadline);
        return Task.CompletedTask;
    }
}
