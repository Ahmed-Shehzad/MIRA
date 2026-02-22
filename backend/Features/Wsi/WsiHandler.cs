using Microsoft.EntityFrameworkCore;
using HiveOrders.Api.Shared.Data;
using HiveOrders.Api.Shared.Events;
using HiveOrders.Api.Shared.Infrastructure;
using HiveOrders.Api.Shared.ValueObjects;
using MassTransit;

namespace HiveOrders.Api.Features.Wsi;

public class WsiHandler : IWsiHandler
{
    private const int MaxUploadsPerUser = 100;

    private readonly ApplicationDbContext _db;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ITenantContext _tenantContext;
    private readonly IS3PresignedUrlService _s3Service;

    public WsiHandler(
        ApplicationDbContext db,
        IPublishEndpoint publishEndpoint,
        ITenantContext tenantContext,
        IS3PresignedUrlService s3Service)
    {
        _db = db;
        _publishEndpoint = publishEndpoint;
        _tenantContext = tenantContext;
        _s3Service = s3Service;
    }

    public async Task<WsiPresignedUrlResponse?> GetPresignedUploadUrlAsync(WsiPresignedUrlRequest request, UserId userId, CancellationToken cancellationToken = default)
    {
        var key = $"wsi/{userId.Value}/{Guid.NewGuid():N}/{request.FileName}";
        var url = await _s3Service.GetUploadUrlAsync(key, request.ContentType ?? "application/octet-stream", cancellationToken);
        return url == null ? null : new WsiPresignedUrlResponse(url, key);
    }

    public async Task<WsiUploadResponse?> CreateUploadAsync(CreateWsiUploadRequest request, UserId userId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.TenantId ?? throw new UnauthorizedAccessException("Tenant context required.");

        var count = await _db.WsiUploads
            .CountAsync(u => u.TenantId == tenantId.Value && u.UploadedByUserId == userId, cancellationToken);
        if (count >= MaxUploadsPerUser)
            throw new InvalidOperationException($"Maximum {MaxUploadsPerUser} WSI uploads per user.");

        var upload = new WsiUpload
        {
            Id = new WsiUploadId(Guid.NewGuid()),
            TenantId = tenantId.Value,
            UploadedByUserId = userId,
            S3Key = request.S3Key,
            FileName = request.FileName,
            ContentType = request.ContentType,
            FileSizeBytes = request.FileSizeBytes,
            WidthPx = request.WidthPx,
            HeightPx = request.HeightPx,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _db.WsiUploads.Add(upload);
        await _db.SaveChangesAsync(cancellationToken);

        return MapToUploadResponse(upload);
    }

    public async Task<WsiUploadResponse?> GetUploadAsync(WsiUploadId id, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.TenantId;
        if (tenantId == null) return null;

        var upload = await _db.WsiUploads
            .FirstOrDefaultAsync(u => u.Id == id && u.TenantId == tenantId.Value, cancellationToken);
        return upload == null ? null : MapToUploadResponse(upload);
    }

    public async Task<IReadOnlyList<WsiUploadResponse>> GetUploadsAsync(UserId userId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.TenantId;
        if (tenantId == null) return [];

        var uploads = await _db.WsiUploads
            .Where(u => u.TenantId == tenantId.Value && u.UploadedByUserId == userId)
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync(cancellationToken);

        return uploads.Select(MapToUploadResponse).ToList();
    }

    public async Task<WsiJobResponse?> TriggerAnalysisAsync(WsiUploadId uploadId, UserId userId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.TenantId ?? throw new UnauthorizedAccessException("Tenant context required.");

        var upload = await _db.WsiUploads
            .FirstOrDefaultAsync(u => u.Id == uploadId && u.TenantId == tenantId.Value, cancellationToken);
        if (upload == null) return null;

        var job = new WsiJob
        {
            Id = new WsiJobId(Guid.NewGuid()),
            WsiUploadId = uploadId,
            TenantId = tenantId.Value,
            RequestedByUserId = userId,
            Status = WsiJobStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _db.WsiJobs.Add(job);
        await _db.SaveChangesAsync(cancellationToken);

        await _publishEndpoint.Publish(new WsiAnalysisRequestedEvent(
            job.Id.Value,
            upload.Id.Value,
            upload.TenantId,
            userId.Value,
            upload.S3Key), cancellationToken);

        return MapToJobResponse(job);
    }

    public async Task<WsiJobResponse?> GetJobAsync(WsiJobId jobId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.TenantId;
        if (tenantId == null) return null;

        var job = await _db.WsiJobs
            .Include(j => j.WsiUpload)
            .FirstOrDefaultAsync(j => j.Id == jobId && j.TenantId == tenantId.Value, cancellationToken);
        return job == null ? null : MapToJobResponse(job);
    }

    private static WsiUploadResponse MapToUploadResponse(WsiUpload u) =>
        new(u.Id.Value, u.S3Key, u.FileName, u.ContentType, u.FileSizeBytes, u.WidthPx, u.HeightPx, u.CreatedAt);

    private static WsiJobResponse MapToJobResponse(WsiJob j) =>
        new(j.Id.Value, j.WsiUploadId.Value, j.Status.Value, j.ResultS3Key, j.ErrorMessage, j.CreatedAt, j.CompletedAt);
}
