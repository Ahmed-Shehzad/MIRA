namespace HiveOrders.Api.Shared.ValueObjects;

/// <summary>Whole Slide Image upload identifier.</summary>
public readonly record struct WsiUploadId
{
    public Guid Value { get; }

    public WsiUploadId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("WsiUploadId cannot be empty.", nameof(value));
        Value = value;
    }

    public override string ToString() => Value.ToString("N");
    public static implicit operator Guid(WsiUploadId id) => id.Value;
    public static explicit operator WsiUploadId(Guid value) => new(value);
}
