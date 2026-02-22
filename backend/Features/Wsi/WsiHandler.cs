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
    private const int MaxFileNameLength = 256;
    private const int MaxS3KeyLength = 512;
    private const long DefaultMaxSinglePutBytes = 5L * 1024 * 1024 * 1024; // 5 GB

    private readonly ApplicationDbContext _db;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ITenantContext _tenantContext;
    private readonly IS3PresignedUrlService _s3Service;
    private readonly IConfiguration _configuration;

    public WsiHandler(
        ApplicationDbContext db,
        IPublishEndpoint publishEndpoint,
        ITenantContext tenantContext,
        IS3PresignedUrlService s3Service,
        IConfiguration configuration)
    {
        _db = db;
        _publishEndpoint = publishEndpoint;
        _tenantContext = tenantContext;
        _s3Service = s3Service;
        _configuration = configuration;
    }

    public async Task<WsiPresignedUrlResponse?> GetPresignedUploadUrlAsync(WsiPresignedUrlRequest request, UserId userId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.TenantId ?? throw new UnauthorizedAccessException("Tenant context required.");

        ValidateFileName(request.FileName);
        ValidateFileSize(request.FileSizeBytes);

        var count = await _db.WsiUploads
            .CountAsync(u => u.TenantId == tenantId.Value && u.UploadedByUserId == userId, cancellationToken);
        if (count >= MaxUploadsPerUser)
            throw new InvalidOperationException($"Maximum {MaxUploadsPerUser} WSI uploads per user.");

        var key = $"wsi/{userId.Value}/{Guid.NewGuid():N}/{SanitizeFileName(request.FileName)}";
        if (key.Length > MaxS3KeyLength)
            throw new ArgumentException($"Generated S3 key exceeds maximum length of {MaxS3KeyLength}.", nameof(request));

        var url = await _s3Service.GetUploadUrlAsync(key, request.ContentType ?? "application/octet-stream", expiration: null, cancellationToken);
        if (url == null)
            return null;

        var upload = new WsiUpload
        {
            Id = new WsiUploadId(Guid.NewGuid()),
            TenantId = tenantId.Value,
            UploadedByUserId = userId,
            S3Key = key,
            FileName = request.FileName,
            ContentType = request.ContentType,
            FileSizeBytes = request.FileSizeBytes,
            WidthPx = null,
            HeightPx = null,
            Status = WsiUploadStatusValues.Uploading,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _db.WsiUploads.Add(upload);
        await _db.SaveChangesAsync(cancellationToken);

        return new WsiPresignedUrlResponse(url, key, upload.Id.Value);
    }

    public async Task<WsiUploadResponse?> ConfirmUploadAsync(WsiUploadId id, UserId userId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.TenantId ?? throw new UnauthorizedAccessException("Tenant context required.");

        var upload = await _db.WsiUploads
            .FirstOrDefaultAsync(u => u.Id == id && u.TenantId == tenantId.Value && u.UploadedByUserId == userId, cancellationToken);
        if (upload == null)
            return null;

        if (upload.Status == WsiUploadStatusValues.Ready)
            return MapToUploadResponse(upload);

        var exists = await _s3Service.ObjectExistsAsync(upload.S3Key, cancellationToken);
        if (!exists)
        {
            _db.WsiUploads.Remove(upload);
            await _db.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("S3 object not found. Upload may have failed or expired.");
        }

        upload.Status = WsiUploadStatusValues.Ready;
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
        if (upload == null)
            return null;

        if (upload.Status != WsiUploadStatusValues.Ready)
            throw new InvalidOperationException("Upload must be confirmed (Ready) before analysis can be triggered.");

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

    private static void ValidateFileName(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        if (fileName.Length > MaxFileNameLength)
            throw new ArgumentException($"File name must not exceed {MaxFileNameLength} characters.", nameof(fileName));
    }

    private void ValidateFileSize(long fileSizeBytes)
    {
        if (fileSizeBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(fileSizeBytes), "File size must be non-negative.");

        var maxBytes = _configuration.GetValue("AWS:S3:MaxSinglePutBytes", DefaultMaxSinglePutBytes);
        if (fileSizeBytes > maxBytes)
            throw new ArgumentOutOfRangeException(nameof(fileSizeBytes), $"File size exceeds maximum of {maxBytes / (1024 * 1024)} MB for single upload.");
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", fileName.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
    }

    private static WsiUploadResponse MapToUploadResponse(WsiUpload u) =>
        new(u.Id.Value, u.S3Key, u.FileName, u.ContentType, u.FileSizeBytes, u.WidthPx, u.HeightPx, u.Status, u.CreatedAt);

    private static WsiJobResponse MapToJobResponse(WsiJob j) =>
        new(j.Id.Value, j.WsiUploadId.Value, j.Status.Value, j.ResultS3Key, j.ErrorMessage, j.CreatedAt, j.CompletedAt);
}
