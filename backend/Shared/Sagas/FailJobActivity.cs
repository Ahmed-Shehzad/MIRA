using HiveOrders.Api.Shared.Data;
using HiveOrders.Api.Shared.Events;
using HiveOrders.Api.Shared.ValueObjects;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace HiveOrders.Api.Shared.Sagas;

public class FailJobActivity : IStateMachineActivity<WsiAnalysisSagaState, WsiAnalysisFailedEvent>
{
    private readonly ApplicationDbContext _db;

    public FailJobActivity(ApplicationDbContext db) => _db = db;

    public void Probe(ProbeContext context) => context.CreateScope("fail-job");

    public void Accept(StateMachineVisitor visitor) => visitor.Visit(this);

    public async Task Execute(
        BehaviorContext<WsiAnalysisSagaState, WsiAnalysisFailedEvent> context,
        IBehavior<WsiAnalysisSagaState, WsiAnalysisFailedEvent> next)
    {
        var job = await _db.WsiJobs
            .FirstOrDefaultAsync(j => j.Id == new WsiJobId(context.Saga.CorrelationId), context.CancellationToken);
        if (job != null)
        {
            job.Status = WsiJobStatus.Failed;
            job.ErrorMessage = context.Message.ErrorMessage;
            job.CompletedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(context.CancellationToken);
        }

        await next.Execute(context);
    }

    public Task Faulted<TException>(
        BehaviorExceptionContext<WsiAnalysisSagaState, WsiAnalysisFailedEvent, TException> context,
        IBehavior<WsiAnalysisSagaState, WsiAnalysisFailedEvent> next)
        where TException : Exception
        => next.Faulted(context);
}
