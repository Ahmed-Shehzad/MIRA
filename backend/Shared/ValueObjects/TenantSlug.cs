namespace HiveOrders.Api.Shared.ValueObjects;

/// <summary>Tenant URL slug (unique per tenant).</summary>
public readonly record struct TenantSlug
{
    public string Value { get; }

    public TenantSlug(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length > 50)
            throw new ArgumentException("TenantSlug must not exceed 50 characters.", nameof(value));
        Value = value;
    }

    public override string ToString() => Value;
    public static implicit operator string(TenantSlug s) => s.Value;
    public static explicit operator TenantSlug(string value) => new(value);
}
