namespace HiveOrders.Api.Shared.ValueObjects;

/// <summary>User group/role (e.g. Admins, Managers, Users).</summary>
public readonly record struct UserGroup
{
    public string Value { get; }

    public UserGroup(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public override string ToString() => Value;
    public static implicit operator string(UserGroup g) => g.Value;
    public static explicit operator UserGroup(string value) => new(value);
}
