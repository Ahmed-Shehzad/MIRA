using Microsoft.EntityFrameworkCore;
using HiveOrders.Api.Shared.Identity;
using HiveOrders.Api.Shared.ValueObjects;

namespace HiveOrders.Api.Shared.Data;

public static class DbInitializer
{
    public const string GroupUsers = "Users";
    public const string GroupManagers = "Managers";
    public const string GroupAdmins = "Admins";

    public const string DefaultTenantSlug = "hive";

    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var defaultSlug = (TenantSlug)DefaultTenantSlug;
        var defaultTenant = await db.Tenants.FirstOrDefaultAsync(t => t.Slug == defaultSlug);
        if (defaultTenant == null)
        {
            db.Tenants.Add(new Tenant { Name = "HIVE", Slug = defaultSlug });
            await db.SaveChangesAsync();
        }
    }
}
