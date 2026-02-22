namespace HiveOrders.Api.Shared.ValueObjects;

/// <summary>Order item identifier.</summary>
public readonly record struct OrderItemId
{
    public int Value { get; }

    public OrderItemId(int value)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(nameof(value), "OrderItemId must be non-negative.");
        Value = value;
    }

    public override string ToString() => Value.ToString();
    public static implicit operator int(OrderItemId id) => id.Value;
    public static explicit operator OrderItemId(int value) => new(value);
}
