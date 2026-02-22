using HiveOrders.Api.Shared.Data;
using HiveOrders.Api.Shared.Events;
using HiveOrders.Api.Shared.ValueObjects;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace HiveOrders.Api.Shared.Sagas;

public class SetJobProcessingActivity : IStateMachineActivity<WsiAnalysisSagaState, WsiAnalysisRequestedEvent>
{
    private readonly ApplicationDbContext _db;

    public SetJobProcessingActivity(ApplicationDbContext db) => _db = db;

    public void Probe(ProbeContext context) => context.CreateScope("set-job-processing");

    public void Accept(StateMachineVisitor visitor) => visitor.Visit(this);

    public async Task Execute(
        BehaviorContext<WsiAnalysisSagaState, WsiAnalysisRequestedEvent> context,
        IBehavior<WsiAnalysisSagaState, WsiAnalysisRequestedEvent> next)
    {
        var job = await _db.WsiJobs
            .FirstOrDefaultAsync(j => j.Id == new WsiJobId(context.Saga.CorrelationId), context.CancellationToken);
        if (job != null)
        {
            job.Status = WsiJobStatus.Processing;
            await _db.SaveChangesAsync(context.CancellationToken);
        }

        await next.Execute(context);
    }

    public Task Faulted<TException>(
        BehaviorExceptionContext<WsiAnalysisSagaState, WsiAnalysisRequestedEvent, TException> context,
        IBehavior<WsiAnalysisSagaState, WsiAnalysisRequestedEvent> next)
        where TException : Exception
        => next.Faulted(context);
}
