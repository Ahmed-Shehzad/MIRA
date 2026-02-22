namespace HiveOrders.Api.Shared.ValueObjects;

/// <summary>External system identifier (e.g. Teams user ID).</summary>
public readonly record struct ExternalId
{
    public string Value { get; }

    public ExternalId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public override string ToString() => Value;
    public static implicit operator string(ExternalId id) => id.Value;
    public static explicit operator ExternalId(string value) => new(value);
}
