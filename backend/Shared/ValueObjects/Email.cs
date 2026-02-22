namespace HiveOrders.Api.Shared.ValueObjects;

/// <summary>Email address.</summary>
public readonly record struct Email
{
    public string Value { get; }

    public Email(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length > 256)
            throw new ArgumentException("Email must not exceed 256 characters.", nameof(value));
        Value = value;
    }

    public override string ToString() => Value;
    public static implicit operator string(Email e) => e.Value;
    public static explicit operator Email(string value) => new(value);
}
