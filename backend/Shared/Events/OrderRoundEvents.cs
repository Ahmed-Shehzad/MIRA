namespace HiveOrders.Api.Shared.Events;

public record OrderRoundCreatedEvent(
    int OrderRoundId,
    string RestaurantName,
    string CreatedByUserId,
    DateTime Deadline);

public record OrderItemAddedEvent(
    int OrderRoundId,
    int OrderItemId,
    string UserId,
    string Description,
    decimal Price);

public record OrderRoundClosedEvent(
    int OrderRoundId,
    string CreatedByUserId);

public record PaymentCompletedEvent(
    int PaymentId,
    int OrderRoundId,
    string UserId,
    decimal Amount);
