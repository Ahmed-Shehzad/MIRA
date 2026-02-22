using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace HiveOrders.Api.Shared.Infrastructure;

public sealed class TenantContext : ITenantContext
{
    private const string TenantIdClaim = "tenant_id";
    private const string RoleAdmin = "Admin";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public int? TenantId
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?.User?.FindFirst(TenantIdClaim)?.Value;
            return int.TryParse(claim, out var id) ? id : null;
        }
    }

    public bool IsAdmin => _httpContextAccessor.HttpContext?.User?.IsInRole(RoleAdmin) ?? false;
}
