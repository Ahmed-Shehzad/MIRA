namespace HiveOrders.Api.Shared.ValueObjects;

/// <summary>Order round identifier.</summary>
public readonly record struct OrderRoundId
{
    public int Value { get; }

    public OrderRoundId(int value)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(nameof(value), "OrderRoundId must be non-negative.");
        Value = value;
    }

    public override string ToString() => Value.ToString();
    public static implicit operator int(OrderRoundId id) => id.Value;
    public static explicit operator OrderRoundId(int value) => new(value);
}
