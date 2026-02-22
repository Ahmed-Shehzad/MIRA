namespace HiveOrders.Api.Shared.ValueObjects;

/// <summary>Order round lifecycle status.</summary>
public readonly record struct OrderRoundStatus
{
    public string Value { get; }

    private OrderRoundStatus(string value) => Value = value;

    public static readonly OrderRoundStatus Open = new("Open");
    public static readonly OrderRoundStatus Closed = new("Closed");

    public override string ToString() => Value;
    public static implicit operator string(OrderRoundStatus s) => s.Value;
}
