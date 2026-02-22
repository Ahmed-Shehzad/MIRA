using MassTransit;

namespace HiveOrders.Api.Shared.Sagas;

/// <summary>Saga state for WSI analysis orchestration. Correlated by JobId.</summary>
public class WsiAnalysisSagaState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; } = null!;
    public byte[]? RowVersion { get; set; }

    public Guid UploadId { get; set; }
    public int TenantId { get; set; }
    public string RequestedByUserId { get; set; } = null!;
    public string S3Key { get; set; } = null!;
}
