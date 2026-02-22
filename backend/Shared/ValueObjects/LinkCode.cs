namespace HiveOrders.Api.Shared.ValueObjects;

/// <summary>Bot link code (6-digit).</summary>
public readonly record struct LinkCode
{
    public string Value { get; }

    public LinkCode(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length != 6 || !value.All(char.IsDigit))
            throw new ArgumentException("LinkCode must be exactly 6 digits.", nameof(value));
        Value = value;
    }

    public override string ToString() => Value;
    public static implicit operator string(LinkCode c) => c.Value;
    public static explicit operator LinkCode(string value) => new(value);
}
