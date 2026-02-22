using System.Text.Json.Serialization;

namespace HiveOrders.Api.Features.Wsi;

public record WsiPresignedUrlRequest(string FileName, string? ContentType, [property: JsonRequired] long FileSizeBytes);

public record WsiPresignedUrlResponse(string Url, string Key, Guid UploadId);

public record WsiUploadResponse(
    Guid Id,
    string S3Key,
    string FileName,
    string? ContentType,
    long FileSizeBytes,
    int? WidthPx,
    int? HeightPx,
    string Status,
    DateTimeOffset CreatedAt);

public record WsiJobResponse(
    Guid Id,
    Guid WsiUploadId,
    string Status,
    string? ResultS3Key,
    string? ErrorMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt);
