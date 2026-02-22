namespace HiveOrders.Api.Shared.Infrastructure;

/// <summary>Service for generating presigned S3 URLs for upload/download. Used in production per DOCUMENTATION.md.</summary>
public interface IS3PresignedUrlService
{
    /// <summary>Generates a presigned PUT URL for uploading a file. Returns null if S3 is not configured.</summary>
    Task<string?> GetUploadUrlAsync(string key, string contentType, TimeSpan? expiration = null, CancellationToken cancellationToken = default);

    /// <summary>Generates a presigned GET URL for downloading a file. Returns null if S3 is not configured.</summary>
    Task<string?> GetDownloadUrlAsync(string key, TimeSpan? expiration = null, CancellationToken cancellationToken = default);

    /// <summary>Checks if an object exists in S3. Returns false if S3 is not configured or object not found.</summary>
    Task<bool> ObjectExistsAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Deletes an object from S3. Returns false if S3 is not configured. Ignores 404.</summary>
    Task<bool> TryDeleteObjectAsync(string key, CancellationToken cancellationToken = default);
}
