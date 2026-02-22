using HiveOrders.Api.Shared.Infrastructure;
using HiveOrders.Api.Shared.ValueObjects;

namespace HiveOrders.Api.Features.Storage;

public class StorageHandler : IStorageHandler
{
    private readonly IS3PresignedUrlService _s3Service;

    public StorageHandler(IS3PresignedUrlService s3Service)
    {
        _s3Service = s3Service;
    }

    public async Task<PresignedUrlResponse?> GetPresignedUploadUrlAsync(PresignedUrlRequest request, UserId userId, CancellationToken cancellationToken = default)
    {
        var key = $"uploads/{userId.Value}/{Guid.NewGuid():N}/{request.FileName}";
        var url = await _s3Service.GetUploadUrlAsync(key, request.ContentType ?? "application/octet-stream", cancellationToken);
        return url == null ? null : new PresignedUrlResponse(url, key);
    }
}
