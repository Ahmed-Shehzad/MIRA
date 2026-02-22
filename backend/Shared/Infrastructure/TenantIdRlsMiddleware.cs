using Microsoft.EntityFrameworkCore;
using HiveOrders.Api.Shared.Data;

namespace HiveOrders.Api.Shared.Infrastructure;

/// <summary>
/// Sets app.tenant_id for PostgreSQL RLS before any request that uses the database.
/// Must run after auth middleware so TenantContext has the tenant from JWT.
/// </summary>
public sealed class TenantIdRlsMiddleware
{
    private readonly RequestDelegate _next;

    public TenantIdRlsMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext, ApplicationDbContext db)
    {
        var tenantId = tenantContext.TenantId;
        if (tenantId.HasValue)
        {
            await db.Database.ExecuteSqlRawAsync(
                "SELECT set_config('app.tenant_id', {0}, false)",
                tenantId.Value.Value.ToString());
        }

        await _next(context);
    }
}
