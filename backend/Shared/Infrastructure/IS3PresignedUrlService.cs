namespace HiveOrders.Api.Shared.Infrastructure;

/// <summary>Service for generating presigned S3 URLs for upload/download. Used in production per high_level_platform.md.</summary>
public interface IS3PresignedUrlService
{
    /// <summary>Generates a presigned PUT URL for uploading a file. Returns null if S3 is not configured.</summary>
    Task<string?> GetUploadUrlAsync(string key, string contentType, CancellationToken cancellationToken = default);

    /// <summary>Generates a presigned GET URL for downloading a file. Returns null if S3 is not configured.</summary>
    Task<string?> GetDownloadUrlAsync(string key, TimeSpan? expiration = null, CancellationToken cancellationToken = default);
}
