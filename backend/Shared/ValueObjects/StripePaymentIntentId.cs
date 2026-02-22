namespace HiveOrders.Api.Shared.ValueObjects;

/// <summary>Stripe payment intent identifier.</summary>
public readonly record struct StripePaymentIntentId
{
    public string Value { get; }

    public StripePaymentIntentId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length > 100)
            throw new ArgumentException("StripePaymentIntentId must not exceed 100 characters.", nameof(value));
        Value = value;
    }

    public override string ToString() => Value;
    public static implicit operator string(StripePaymentIntentId id) => id.Value;
    public static explicit operator StripePaymentIntentId(string value) => new(value);
}
