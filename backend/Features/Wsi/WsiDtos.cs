using System.Text.Json.Serialization;

namespace HiveOrders.Api.Features.Wsi;

public record CreateWsiUploadRequest(
    string S3Key,
    string FileName,
    string? ContentType,
    [property: JsonRequired] long FileSizeBytes,
    int? WidthPx,
    int? HeightPx);

public record WsiUploadResponse(
    Guid Id,
    string S3Key,
    string FileName,
    string? ContentType,
    long FileSizeBytes,
    int? WidthPx,
    int? HeightPx,
    DateTimeOffset CreatedAt);

public record WsiJobResponse(
    Guid Id,
    Guid WsiUploadId,
    string Status,
    string? ResultS3Key,
    string? ErrorMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt);
