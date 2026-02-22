namespace HiveOrders.Api.Shared.ValueObjects;

/// <summary>Multi-tenant identifier.</summary>
public readonly record struct TenantId
{
    public int Value { get; }

    public TenantId(int value)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(nameof(value), "TenantId must be non-negative.");
        Value = value;
    }

    public override string ToString() => Value.ToString();
    public static implicit operator int(TenantId id) => id.Value;
    public static explicit operator TenantId(int value) => new(value);
}
