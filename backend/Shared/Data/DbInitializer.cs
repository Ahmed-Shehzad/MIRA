using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using HiveOrders.Api.Shared.Identity;

namespace HiveOrders.Api.Shared.Data;

public static class DbInitializer
{
    public const string RoleUser = "User";
    public const string RoleManager = "Manager";
    public const string RoleAdmin = "Admin";

    public const string DefaultTenantSlug = "hive";

    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        foreach (var roleName in new[] { RoleUser, RoleManager, RoleAdmin })
        {
            if (!await roleManager.RoleExistsAsync(roleName))
                await roleManager.CreateAsync(new IdentityRole(roleName));
        }

        var defaultTenant = await db.Tenants.FirstOrDefaultAsync(t => t.Slug == DefaultTenantSlug);
        if (defaultTenant == null)
        {
            db.Tenants.Add(new Tenant { Name = "HIVE", Slug = DefaultTenantSlug });
            await db.SaveChangesAsync();
        }
    }
}
