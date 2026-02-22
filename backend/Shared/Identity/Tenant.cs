using System.ComponentModel.DataAnnotations;

namespace HiveOrders.Api.Shared.Identity;

public class Tenant
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public required string Name { get; set; }

    [Required]
    [MaxLength(50)]
    public required string Slug { get; set; }

    public bool IsActive { get; set; } = true;
}
