using System.ComponentModel.DataAnnotations;
using HiveOrders.Api.Shared.Identity;
using HiveOrders.Api.Shared.ValueObjects;

namespace HiveOrders.Api.Features.Wsi;

using static WsiUploadStatusValues;

/// <summary>Whole Slide Image upload metadata. Per DOCUMENTATION.md Phase 1 MVP.</summary>
public class WsiUpload
{
    public WsiUploadId Id { get; set; }

    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public UserId UploadedByUserId { get; set; }
    public AppUser UploadedByUser { get; set; } = null!;

    [Required]
    [MaxLength(512)]
    public required string S3Key { get; set; }

    [Required]
    [MaxLength(256)]
    public required string FileName { get; set; }

    [MaxLength(64)]
    public string? ContentType { get; set; }

    public long FileSizeBytes { get; set; }

    public int? WidthPx { get; set; }
    public int? HeightPx { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    [Required]
    [MaxLength(16)]
    public string Status { get; set; } = Uploading;

    public ICollection<WsiJob> Jobs { get; set; } = new List<WsiJob>();
}
