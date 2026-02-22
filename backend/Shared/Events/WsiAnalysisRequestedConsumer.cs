using HiveOrders.Api.Shared.Infrastructure;
using MassTransit;

namespace HiveOrders.Api.Shared.Events;

/// <summary>
/// Mock consumer for WSI analysis. Simulates GPU worker completion for dev/demo.
/// In production, replace with external Python/GPU service that consumes from SQS.
/// </summary>
public class WsiAnalysisRequestedConsumer : IConsumer<WsiAnalysisRequestedEvent>
{
    private readonly IConfiguration _configuration;

    public WsiAnalysisRequestedConsumer(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task Consume(ConsumeContext<WsiAnalysisRequestedEvent> context)
    {
        var msg = context.Message;
        var useMock = string.Equals(_configuration["Wsi:UseMockWorker"], "true", StringComparison.OrdinalIgnoreCase);

        if (!useMock)
            return;

        await Task.Delay(500, context.CancellationToken);

        await context.Publish(new WsiAnalysisCompletedEvent(
            msg.JobId,
            $"results/{msg.TenantId}/{msg.UploadId}/analysis.json"));
    }
}
