using HiveOrders.Api.Shared.Data;
using HiveOrders.Api.Shared.Events;
using HiveOrders.Api.Shared.ValueObjects;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace HiveOrders.Api.Shared.Sagas;

public class WsiAnalysisSaga : MassTransitStateMachine<WsiAnalysisSagaState>
{
    public WsiAnalysisSaga()
    {
        InstanceState(x => x.CurrentState);

        Event(() => AnalysisRequested, e => e.CorrelateById(m => m.Message.JobId));
        Event(() => AnalysisCompleted, e => e.CorrelateById(m => m.Message.JobId));
        Event(() => AnalysisFailed, e => e.CorrelateById(m => m.Message.JobId));

        Initially(
            When(AnalysisRequested)
                .Then(c => StoreSagaData(c))
                .Activity(x => x.OfType<SetJobProcessingActivity>())
                .TransitionTo(Processing));

        During(Processing,
            When(AnalysisCompleted)
                .Activity(x => x.OfType<CompleteJobActivity>())
                .Finalize(),
            When(AnalysisFailed)
                .Activity(x => x.OfType<FailJobActivity>())
                .Finalize());
    }

    private static void StoreSagaData(BehaviorContext<WsiAnalysisSagaState, WsiAnalysisRequestedEvent> context)
    {
        context.Saga.UploadId = context.Message.UploadId;
        context.Saga.TenantId = context.Message.TenantId;
        context.Saga.RequestedByUserId = context.Message.RequestedByUserId;
        context.Saga.S3Key = context.Message.S3Key;
    }

    public State Processing { get; } = null!;

    public Event<WsiAnalysisRequestedEvent> AnalysisRequested { get; } = null!;
    public Event<WsiAnalysisCompletedEvent> AnalysisCompleted { get; } = null!;
    public Event<WsiAnalysisFailedEvent> AnalysisFailed { get; } = null!;
}
