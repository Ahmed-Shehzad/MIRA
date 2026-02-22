namespace HiveOrders.Api.Shared.ValueObjects;

/// <summary>WSI analysis job status.</summary>
public readonly record struct WsiJobStatus
{
    public string Value { get; }

    private WsiJobStatus(string value) => Value = value;

    public static readonly WsiJobStatus Pending = new("Pending");
    public static readonly WsiJobStatus Processing = new("Processing");
    public static readonly WsiJobStatus Completed = new("Completed");
    public static readonly WsiJobStatus Failed = new("Failed");

    public override string ToString() => Value;
}
