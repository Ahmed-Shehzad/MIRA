using HiveOrders.Api.Shared.Data;
using HiveOrders.Api.Shared.Events;
using HiveOrders.Api.Shared.ValueObjects;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace HiveOrders.Api.Shared.Sagas;

public class CompleteJobActivity : IStateMachineActivity<WsiAnalysisSagaState, WsiAnalysisCompletedEvent>
{
    private readonly ApplicationDbContext _db;

    public CompleteJobActivity(ApplicationDbContext db) => _db = db;

    public void Probe(ProbeContext context) => context.CreateScope("complete-job");

    public void Accept(StateMachineVisitor visitor) => visitor.Visit(this);

    public async Task Execute(
        BehaviorContext<WsiAnalysisSagaState, WsiAnalysisCompletedEvent> context,
        IBehavior<WsiAnalysisSagaState, WsiAnalysisCompletedEvent> next)
    {
        var job = await _db.WsiJobs
            .FirstOrDefaultAsync(j => j.Id == new WsiJobId(context.Saga.CorrelationId), context.CancellationToken);
        if (job != null)
        {
            job.Status = WsiJobStatus.Completed;
            job.ResultS3Key = context.Message.ResultS3Key;
            job.CompletedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(context.CancellationToken);
        }

        await next.Execute(context);
    }

    public Task Faulted<TException>(
        BehaviorExceptionContext<WsiAnalysisSagaState, WsiAnalysisCompletedEvent, TException> context,
        IBehavior<WsiAnalysisSagaState, WsiAnalysisCompletedEvent> next)
        where TException : Exception
        => next.Faulted(context);
}
