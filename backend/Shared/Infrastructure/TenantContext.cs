using Microsoft.AspNetCore.Http;
using HiveOrders.Api.Shared.ValueObjects;

namespace HiveOrders.Api.Shared.Infrastructure;

public sealed class TenantContext : ITenantContext
{
    private const string TenantIdClaim = "tenant_id";
    private const string GroupAdmins = "Admins";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public TenantId? TenantId
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?.User?.FindFirst(TenantIdClaim)?.Value;
            return int.TryParse(claim, out var id) ? (TenantId?)new TenantId(id) : null;
        }
    }

    public bool IsAdmin => _httpContextAccessor.HttpContext?.User?.IsInRole(GroupAdmins) ?? false;
}
