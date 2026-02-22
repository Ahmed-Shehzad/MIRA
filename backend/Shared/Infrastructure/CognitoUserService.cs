using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Microsoft.EntityFrameworkCore;
using HiveOrders.Api.Shared.Data;
using HiveOrders.Api.Shared.Identity;
using HiveOrders.Api.Shared.ValueObjects;

namespace HiveOrders.Api.Shared.Infrastructure;

public sealed class CognitoUserService : ICognitoUserService
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly IAmazonCognitoIdentityProvider _cognitoClient;

    public CognitoUserService(
        ApplicationDbContext db,
        IConfiguration configuration,
        IAmazonCognitoIdentityProvider cognitoClient)
    {
        _db = db;
        _configuration = configuration;
        _cognitoClient = cognitoClient;
    }

    public async Task<AppUser?> ProvisionOrFindAsync(
        string cognitoSub,
        string? email,
        string? cognitoUsername,
        IReadOnlyList<string> cognitoGroups,
        string? customTenantId,
        string? customCompany,
        CancellationToken cancellationToken = default)
    {
        var uid = (UserId)cognitoSub;
        var existing = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == uid, cancellationToken);

        if (existing != null)
            return existing;

        var tenantId = await ResolveTenantIdAsync(customTenantId, cancellationToken);
        var company = customCompany ?? "Unknown";
        var emailValue = email ?? $"{cognitoSub}@cognito.local";
        var groups = cognitoGroups.Count > 0
            ? cognitoGroups.Select(g => (UserGroup)g).ToList()
            : [(UserGroup)DbInitializer.GroupUsers];
        var now = DateTimeOffset.UtcNow;

        var user = new AppUser
        {
            Id = uid,
            CognitoUsername = cognitoUsername ?? cognitoSub,
            Email = (Email)emailValue,
            Company = company,
            TenantId = tenantId,
            Groups = groups,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);
        return user;
    }

    public async Task<bool> AddUserToGroupAsync(string cognitoUsername, string groupName, CancellationToken cancellationToken = default)
    {
        var userPoolId = _configuration["AWS:Cognito:UserPoolId"];
        if (string.IsNullOrWhiteSpace(userPoolId))
            return false;

        try
        {
            await _cognitoClient.AdminAddUserToGroupAsync(new AdminAddUserToGroupRequest
            {
                UserPoolId = userPoolId,
                Username = cognitoUsername,
                GroupName = groupName
            }, cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<int> ResolveTenantIdAsync(string? customTenantId, CancellationToken cancellationToken)
    {
        if (int.TryParse(customTenantId, out var id))
        {
            var tid = (TenantId)id;
            var exists = await _db.Tenants.AnyAsync(t => t.Id == tid, cancellationToken);
            if (exists) return id;
        }

        var defaultSlug = (TenantSlug)DbInitializer.DefaultTenantSlug;
        var defaultTenant = await _db.Tenants
            .FirstOrDefaultAsync(t => t.Slug == defaultSlug, cancellationToken);

        return defaultTenant?.Id ?? throw new InvalidOperationException(
            $"Tenant not found. Configure custom:tenant_id in Cognito or ensure default tenant '{DbInitializer.DefaultTenantSlug}' exists.");
    }

}
