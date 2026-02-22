namespace HiveOrders.Api.Shared.ValueObjects;

/// <summary>Web Push subscription endpoint URL.</summary>
public readonly record struct PushEndpoint
{
    public string Value { get; }

    public PushEndpoint(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length > 2000)
            throw new ArgumentException("PushEndpoint must not exceed 2000 characters.", nameof(value));
        Value = value;
    }

    public override string ToString() => Value;
    public static implicit operator string(PushEndpoint e) => e.Value;
    public static explicit operator PushEndpoint(string value) => new(value);
}
