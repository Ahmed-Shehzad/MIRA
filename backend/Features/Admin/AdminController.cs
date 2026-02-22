using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HiveOrders.Api.Shared.Data;
using HiveOrders.Api.Shared.Identity;

namespace HiveOrders.Api.Features.Admin;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin")]
[Authorize(Roles = DbInitializer.RoleAdmin)]
public class AdminController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public AdminController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    /// <summary>List all users. Admin only.</summary>
    [HttpGet("users")]
    [ProducesResponseType(typeof(IReadOnlyList<AdminUserResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AdminUserResponse>>> GetUsers(CancellationToken cancellationToken)
    {
        var users = await _db.Users
            .Include(u => u.Tenant)
            .OrderBy(u => u.Email)
            .ToListAsync(cancellationToken);

        var result = new List<AdminUserResponse>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            result.Add(new AdminUserResponse(
                user.Id,
                user.Email!,
                user.Company,
                user.TenantId,
                user.Tenant?.Name ?? "",
                roles));
        }

        return Ok(result);
    }

    /// <summary>List all tenants. Admin only.</summary>
    [HttpGet("tenants")]
    [ProducesResponseType(typeof(IReadOnlyList<TenantResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TenantResponse>>> GetTenants(CancellationToken cancellationToken)
    {
        var tenants = await _db.Tenants
            .OrderBy(t => t.Name)
            .Select(t => new TenantResponse(t.Id, t.Name, t.Slug, t.IsActive))
            .ToListAsync(cancellationToken);
        return Ok(tenants);
    }

    /// <summary>Assign Admin role to a user. Admin only.</summary>
    [HttpPost("users/{userId}/assign-admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignAdmin(string userId, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return NotFound();

        if (await _userManager.IsInRoleAsync(user, DbInitializer.RoleAdmin))
            return NoContent();

        await _userManager.AddToRoleAsync(user, DbInitializer.RoleAdmin);
        return NoContent();
    }
}

public record AdminUserResponse(string Id, string Email, string Company, int TenantId, string TenantName, IList<string> Roles);
public record TenantResponse(int Id, string Name, string Slug, bool IsActive);
