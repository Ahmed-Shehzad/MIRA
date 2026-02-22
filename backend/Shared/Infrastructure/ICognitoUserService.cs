using HiveOrders.Api.Shared.Identity;

namespace HiveOrders.Api.Shared.Infrastructure;

/// <summary>
/// Provisions AppUser from Cognito token claims and performs Cognito admin operations.
/// </summary>
public interface ICognitoUserService
{
    /// <summary>
    /// Provisions or finds an AppUser for the given Cognito claims. Lazy provisioning.
    /// </summary>
    Task<AppUser?> ProvisionOrFindAsync(
        string cognitoSub,
        string? email,
        string? cognitoUsername,
        IReadOnlyList<string> cognitoGroups,
        string? customTenantId,
        string? customCompany,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a user to a Cognito group (e.g. Admin). Requires AWSSDK.CognitoIdentityProvider.
    /// </summary>
    Task<bool> AddUserToGroupAsync(string cognitoUsername, string groupName, CancellationToken cancellationToken = default);
}
