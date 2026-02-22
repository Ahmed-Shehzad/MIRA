using HiveOrders.Api.Shared.ValueObjects;

namespace HiveOrders.Api.Features.Storage;

public interface IStorageHandler
{
    Task<PresignedUrlResponse?> GetPresignedUploadUrlAsync(PresignedUrlRequest request, UserId userId, CancellationToken cancellationToken = default);
}
