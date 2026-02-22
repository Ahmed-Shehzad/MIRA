using Amazon;
using Amazon.S3;
using Amazon.S3.Model;

namespace HiveOrders.Api.Shared.Infrastructure;

public class S3PresignedUrlService : IS3PresignedUrlService
{
    private readonly IAmazonS3? _s3Client;
    private readonly string? _bucketName;
    private readonly TimeSpan _defaultExpiration = TimeSpan.FromMinutes(15);

    public S3PresignedUrlService(IConfiguration configuration)
    {
        var bucketName = configuration["AWS:S3:BucketName"];
        var region = configuration["AWS:S3:Region"] ?? configuration["AWS:Region"];

        if (string.IsNullOrWhiteSpace(bucketName) || string.IsNullOrWhiteSpace(region))
        {
            _s3Client = null;
            _bucketName = null;
            return;
        }

        _bucketName = bucketName;
        var regionEndpoint = RegionEndpoint.GetBySystemName(region);
        _s3Client = new AmazonS3Client(regionEndpoint);
    }

    public async Task<string?> GetUploadUrlAsync(string key, string contentType, CancellationToken cancellationToken = default)
    {
        if (_s3Client == null || _bucketName == null)
            return null;

        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucketName,
            Key = key,
            Verb = HttpVerb.PUT,
            Expires = DateTime.UtcNow.Add(_defaultExpiration)
        };
        request.Headers.ContentType = contentType;

        return await _s3Client.GetPreSignedURLAsync(request);
    }

    public async Task<string?> GetDownloadUrlAsync(string key, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        if (_s3Client == null || _bucketName == null)
            return null;

        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucketName,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(expiration ?? _defaultExpiration)
        };

        return await _s3Client.GetPreSignedURLAsync(request);
    }
}
