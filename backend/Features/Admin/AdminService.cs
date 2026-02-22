using Microsoft.EntityFrameworkCore;
using HiveOrders.Api.Shared.Data;
using HiveOrders.Api.Shared.Identity;
using HiveOrders.Api.Shared.Infrastructure;
using HiveOrders.Api.Shared.ValueObjects;

namespace HiveOrders.Api.Features.Admin;

public class AdminService : IAdminService
{
    private readonly ApplicationDbContext _db;
    private readonly ICognitoUserService _cognitoUserService;

    public AdminService(ApplicationDbContext db, ICognitoUserService cognitoUserService)
    {
        _db = db;
        _cognitoUserService = cognitoUserService;
    }

    public async Task<IReadOnlyList<AdminUserResponse>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Users
            .Include(u => u.Tenant)
            .OrderBy(u => u.Email)
            .Select(u => new AdminUserResponse(
                u.Id.Value,
                u.Email.Value,
                u.Company,
                u.TenantId,
                u.Tenant.Name,
                u.Groups.Select(g => g.Value).ToList()))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TenantResponse>> GetTenantsAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Tenants
            .OrderBy(t => t.Name)
            .Select(t => new TenantResponse(t.Id, t.Name, t.Slug.Value, t.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> AssignAdminAsync(UserId userId, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FindAsync([userId], cancellationToken);
        if (user == null) return false;

        if (user.Groups.Any(g => g.Value == DbInitializer.GroupAdmins))
            return true;

        var added = await _cognitoUserService.AddUserToGroupAsync(user.CognitoUsername ?? user.Id.Value, DbInitializer.GroupAdmins, cancellationToken);
        if (added)
        {
            user.Groups.Add((UserGroup)DbInitializer.GroupAdmins);
            user.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return true;
    }
}
