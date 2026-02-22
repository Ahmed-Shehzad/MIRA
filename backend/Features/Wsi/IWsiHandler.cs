using HiveOrders.Api.Shared.ValueObjects;

namespace HiveOrders.Api.Features.Wsi;

public interface IWsiHandler
{
    Task<WsiPresignedUrlResponse?> GetPresignedUploadUrlAsync(WsiPresignedUrlRequest request, UserId userId, CancellationToken cancellationToken = default);

    Task<WsiUploadResponse?> ConfirmUploadAsync(WsiUploadId id, UserId userId, CancellationToken cancellationToken = default);
    Task<WsiUploadResponse?> GetUploadAsync(WsiUploadId id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WsiUploadResponse>> GetUploadsAsync(UserId userId, CancellationToken cancellationToken = default);
    Task<WsiJobResponse?> TriggerAnalysisAsync(WsiUploadId uploadId, UserId userId, CancellationToken cancellationToken = default);
    Task<WsiJobResponse?> GetJobAsync(WsiJobId jobId, CancellationToken cancellationToken = default);
}
