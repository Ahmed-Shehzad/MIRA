using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HiveOrders.Api.Shared.Data;
using HiveOrders.Api.Shared.ValueObjects;

namespace HiveOrders.Api.Features.Admin;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin")]
[Authorize(Roles = DbInitializer.GroupAdmins)]
public class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;

    public AdminController(IAdminService adminService)
    {
        _adminService = adminService;
    }

    /// <summary>List all users. Admin only.</summary>
    [HttpGet("users")]
    [ProducesResponseType(typeof(IReadOnlyList<AdminUserResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AdminUserResponse>>> GetUsers(CancellationToken cancellationToken)
    {
        var users = await _adminService.GetUsersAsync(cancellationToken);
        return Ok(users);
    }

    /// <summary>List all tenants. Admin only.</summary>
    [HttpGet("tenants")]
    [ProducesResponseType(typeof(IReadOnlyList<TenantResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TenantResponse>>> GetTenants(CancellationToken cancellationToken)
    {
        var tenants = await _adminService.GetTenantsAsync(cancellationToken);
        return Ok(tenants);
    }

    /// <summary>Add user to Admins group in Cognito. Admins only.</summary>
    [HttpPost("users/{userId}/assign-admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignAdmin(string userId, CancellationToken cancellationToken)
    {
        var found = await _adminService.AssignAdminAsync(new UserId(userId), cancellationToken);
        return found ? NoContent() : NotFound();
    }
}

public record AdminUserResponse(string Id, string Email, string Company, int TenantId, string TenantName, IList<string> Groups);
public record TenantResponse(int Id, string Name, string Slug, bool IsActive);
