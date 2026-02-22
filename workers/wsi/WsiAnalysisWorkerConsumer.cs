using System.Net.Http.Json;
using Amazon.S3;
using HiveOrders.Api.Shared.Events;
using MassTransit;

namespace HiveOrders.WsiWorker;

/// <summary>
/// Production WSI analysis consumer. Runs inference (or calls GPU service) and publishes completion.
/// Deploy alongside API when Wsi:UseMockWorker=false. Extend RunInferenceAsync for real ML.
/// </summary>
public class WsiAnalysisWorkerConsumer : IConsumer<WsiAnalysisRequestedEvent>
{
    private readonly ILogger<WsiAnalysisWorkerConsumer> _logger;
    private readonly IConfiguration _configuration;
    private readonly IAmazonS3 _s3;

    public WsiAnalysisWorkerConsumer(
        ILogger<WsiAnalysisWorkerConsumer> logger,
        IConfiguration configuration,
        IAmazonS3 s3)
    {
        _logger = logger;
        _configuration = configuration;
        _s3 = s3;
    }

    public async Task Consume(ConsumeContext<WsiAnalysisRequestedEvent> context)
    {
        var msg = context.Message;
        _logger.LogInformation(
            "Processing WSI job {JobId} for upload {UploadId}, S3 key {S3Key}",
            msg.JobId, msg.UploadId, msg.S3Key);

        try
        {
            var resultS3Key = await RunInferenceAsync(msg, context.CancellationToken);
            await context.Publish(new WsiAnalysisCompletedEvent(msg.JobId, resultS3Key), context.CancellationToken);
            _logger.LogInformation("WSI job {JobId} completed", msg.JobId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WSI job {JobId} failed", msg.JobId);
            await context.Publish(new WsiAnalysisFailedEvent(msg.JobId, ex.Message), context.CancellationToken);
        }
    }

    private async Task<string> RunInferenceAsync(WsiAnalysisRequestedEvent msg, CancellationToken cancellationToken)
    {
        var bucketName = _configuration["AWS:S3:BucketName"];
        if (!string.IsNullOrEmpty(bucketName))
        {
            try
            {
                await _s3.GetObjectMetadataAsync(bucketName, msg.S3Key, cancellationToken);
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("WSI object not found: {Bucket}/{Key}. Failing job.", bucketName, msg.S3Key);
                throw new InvalidOperationException("S3 object not found. Upload may have failed or been deleted.");
            }
        }

        var inferenceUrl = _configuration["Wsi:InferenceServiceUrl"];
        if (!string.IsNullOrEmpty(inferenceUrl))
        {
            using var client = new HttpClient();
            var response = await client.PostAsJsonAsync(
                $"{inferenceUrl.TrimEnd('/')}/analyze",
                new { msg.JobId, msg.UploadId, msg.TenantId, msg.S3Key },
                cancellationToken);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<InferenceResult>(cancellationToken);
            return result?.ResultS3Key ?? $"results/{msg.TenantId}/{msg.UploadId}/analysis.json";
        }

        await Task.Delay(1000, cancellationToken);
        return $"results/{msg.TenantId}/{msg.UploadId}/analysis.json";
    }

    private sealed record InferenceResult(string ResultS3Key);
}
