namespace HiveOrders.Api.Shared.ValueObjects;

/// <summary>Payment lifecycle status.</summary>
public readonly record struct PaymentStatus
{
    public string Value { get; }

    private PaymentStatus(string value) => Value = value;

    public static readonly PaymentStatus Pending = new("Pending");
    public static readonly PaymentStatus Completed = new("Completed");
    public static readonly PaymentStatus Failed = new("Failed");

    public override string ToString() => Value;
    public static implicit operator string(PaymentStatus s) => s.Value;
}
