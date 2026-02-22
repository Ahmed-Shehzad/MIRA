using Microsoft.AspNetCore.Identity;

namespace HiveOrders.Api.Shared.Identity;

public class ApplicationUser : IdentityUser
{
    public required string Company { get; set; }

    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
}
