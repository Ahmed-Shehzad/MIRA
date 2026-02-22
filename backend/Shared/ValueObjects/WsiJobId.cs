namespace HiveOrders.Api.Shared.ValueObjects;

/// <summary>WSI analysis job identifier.</summary>
public readonly record struct WsiJobId
{
    public Guid Value { get; }

    public WsiJobId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("WsiJobId cannot be empty.", nameof(value));
        Value = value;
    }

    public override string ToString() => Value.ToString("N");
    public static implicit operator Guid(WsiJobId id) => id.Value;
    public static explicit operator WsiJobId(Guid value) => new(value);
}
