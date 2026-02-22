namespace HiveOrders.Api.Shared.ValueObjects;

/// <summary>Cognito sub (user identifier).</summary>
public readonly record struct UserId
{
    public string Value { get; }

    public UserId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public override string ToString() => Value;
    public static implicit operator string(UserId id) => id.Value;
    public static explicit operator UserId(string value) => new(value);
}
