using System.ComponentModel.DataAnnotations;
using HiveOrders.Api.Shared.ValueObjects;

namespace HiveOrders.Api.Shared.Identity;

/// <summary>
/// Application user provisioned from AWS Cognito. Id is Cognito sub.
/// Groups are cached from cognito:groups.
/// </summary>
public class AppUser
{
    /// <summary>Cognito sub (user identifier). Primary key.</summary>
    public UserId Id { get; set; }

    /// <summary>Cognito username (cognito:username).</summary>
    [MaxLength(256)]
    public string? CognitoUsername { get; set; }

    /// <summary>Email from Cognito.</summary>
    public Email Email { get; set; }

    /// <summary>Company from custom:company or default.</summary>
    [MaxLength(200)]
    public required string Company { get; set; }

    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    /// <summary>Groups cached from cognito:groups (e.g. Admins, Managers, Users).</summary>
    public IList<UserGroup> Groups { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
