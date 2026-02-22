namespace HiveOrders.Api.Shared.ValueObjects;

/// <summary>Notification classification type.</summary>
public readonly record struct NotificationType
{
    public string Value { get; }

    public NotificationType(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length > 50)
            throw new ArgumentException("NotificationType must not exceed 50 characters.", nameof(value));
        Value = value;
    }

    public override string ToString() => Value;
    public static implicit operator string(NotificationType t) => t.Value;
    public static explicit operator NotificationType(string value) => new(value);
}
