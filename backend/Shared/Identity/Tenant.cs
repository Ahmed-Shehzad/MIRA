using System.ComponentModel.DataAnnotations;
using HiveOrders.Api.Shared.ValueObjects;

namespace HiveOrders.Api.Shared.Identity;

public class Tenant
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public required string Name { get; set; }

    public TenantSlug Slug { get; set; }

    public bool IsActive { get; set; } = true;
}
