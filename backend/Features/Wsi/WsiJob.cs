using System.ComponentModel.DataAnnotations;
using HiveOrders.Api.Shared.Identity;
using HiveOrders.Api.Shared.ValueObjects;

namespace HiveOrders.Api.Features.Wsi;

/// <summary>WSI analysis job. Per high_level_platform.md Phase 1 MVP – manual analysis trigger.</summary>
public class WsiJob
{
    public WsiJobId Id { get; set; }

    public WsiUploadId WsiUploadId { get; set; }
    public WsiUpload WsiUpload { get; set; } = null!;

    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public UserId RequestedByUserId { get; set; }
    public AppUser RequestedByUser { get; set; } = null!;

    public WsiJobStatus Status { get; set; } = WsiJobStatus.Pending;

    [MaxLength(2048)]
    public string? ResultS3Key { get; set; }

    [MaxLength(512)]
    public string? ErrorMessage { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
