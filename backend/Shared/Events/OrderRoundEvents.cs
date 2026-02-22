namespace HiveOrders.Api.Shared.Events;

public record OrderRoundCreatedEvent(
    int OrderRoundId,
    string RestaurantName,
    string CreatedByUserId,
    DateTime Deadline);

public record OrderItemAddedEvent(
    int OrderRoundId,
    int OrderItemId,
    string UserId,
    string Description,
    decimal Price);

public record OrderRoundClosedEvent(
    int OrderRoundId,
    string CreatedByUserId);

public record PaymentCompletedEvent(
    int PaymentId,
    int OrderRoundId,
    string UserId,
    decimal Amount);

/// <summary>WSI analysis job requested. Consumed by saga. Per high_level_platform.md Phase 1 MVP.</summary>
public record WsiAnalysisRequestedEvent(
    Guid JobId,
    Guid UploadId,
    int TenantId,
    string RequestedByUserId,
    string S3Key);

/// <summary>Published by GPU worker when analysis completes. Consumed by saga.</summary>
public record WsiAnalysisCompletedEvent(Guid JobId, string ResultS3Key);

/// <summary>Published by GPU worker when analysis fails. Consumed by saga.</summary>
public record WsiAnalysisFailedEvent(Guid JobId, string ErrorMessage);
