namespace HiveOrders.Api.Shared.ValueObjects;

/// <summary>Monetary amount (non-negative).</summary>
public readonly record struct Money
{
    public decimal Value { get; }

    public Money(decimal value)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(nameof(value), "Money must be non-negative.");
        Value = value;
    }

    public override string ToString() => Value.ToString("F2");
    public static implicit operator decimal(Money m) => m.Value;
    public static explicit operator Money(decimal value) => new(value);
}
