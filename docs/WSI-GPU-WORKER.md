# WSI GPU Worker – Production Deployment

> How to deploy a production GPU worker for WSI analysis. Development uses the mock consumer (`Wsi:UseMockWorker=true`). Production requires a separate worker process.

---

## Overview

When `Wsi:UseMockWorker=false` (production), the API's `WsiAnalysisRequestedConsumer` returns early. A **separate worker** must consume `WsiAnalysisRequestedEvent` from SQS, run inference, and publish `WsiAnalysisCompletedEvent` or `WsiAnalysisFailedEvent`.

---

## Option A: .NET Worker Service (Recommended)

Deploy a .NET Worker Service that uses MassTransit and the same SQS/SNS configuration as the API.

### Architecture

```
API (Wsi:UseMockWorker=false)
  → publishes WsiAnalysisRequestedEvent
  → SQS queue (MassTransit)

Worker (separate process)
  → consumes from SQS
  → runs inference (or calls GPU service)
  → publishes WsiAnalysisCompletedEvent
  → Saga receives, updates job, Finalize
```

### Implementation

1. Create a Worker project (e.g. `HiveOrders.WsiWorker`) with:
   - MassTransit + AmazonSQS
   - `WsiAnalysisRequestedConsumer` that:
     - Reads S3 object from `msg.S3Key`
     - Calls inference (Python HTTP/gRPC, or in-process model)
     - Publishes `WsiAnalysisCompletedEvent(jobId, resultS3Key)` or `WsiAnalysisFailedEvent(jobId, errorMessage)`

2. Deploy to ECS Fargate or EKS alongside the API. Use the same AWS config (Region, SQS, SNS).

3. Ensure `Wsi:UseMockWorker=false` in API config.

### Queue Names

MassTransit creates queues based on message types and consumer names. The worker consumes from the same topic/queue as the saga. Configure the worker with the same `AWS:Scope` (e.g. `hive-orders`) so it receives from the correct queues.

---

## Option B: Python Worker (External ML Stack)

For teams using Python for ML inference:

1. **Consume from SQS** – Use boto3 to receive messages. MassTransit uses JSON with envelope; parse `MessageBody` for the event payload.

2. **Message format** – MassTransit SQS messages have structure:
   ```json
   {
     "messageId": "...",
     "messageType": ["urn:message:HiveOrders.Api.Shared.Events:WsiAnalysisRequestedEvent"],
     "payload": {
       "jobId": "guid",
       "uploadId": "guid",
       "tenantId": 1,
       "requestedByUserId": "...",
       "s3Key": "wsi/..."
     }
   }
   ```

3. **Publish completion** – Publish to the SNS topic or SQS queue that the saga consumes. MassTransit expects a specific envelope format. Easiest: use a small .NET helper or Lambda that receives HTTP from Python and publishes via MassTransit.

4. **Alternative** – Use a **bridge queue**: API publishes to a simple SQS queue (e.g. `wsi-jobs-in`). Python consumes, processes, publishes to another queue (`wsi-jobs-out`). A .NET consumer reads from `wsi-jobs-out` and publishes `WsiAnalysisCompletedEvent`. This avoids MassTransit format in Python.

---

## Option C: EKS GPU Pod

For GPU workloads:

1. Build a container with CUDA + inference runtime (e.g. PyTorch, TensorRT).
2. Deploy as Kubernetes Job or Deployment with GPU node selector.
3. Use KEDA to scale based on SQS queue depth.
4. Worker logic: consume from SQS → run inference → publish completion (via Option A or B).

---

## Configuration

| Variable | Worker | Description |
|----------|--------|-------------|
| `AWS__Region` | Yes | Same as API |
| `AWS__AccessKey` / `AWS__SecretKey` | Yes | Or IAM role for ECS/EKS |
| `AWS__Scope` | Yes | Same as API (`hive-orders`) |
| `AWS__S3__BucketName` | Yes | To read WSI files |
| `ConnectionStrings__DefaultConnection` | Yes | To update job status (if worker writes to DB directly; otherwise saga handles it) |

---

## Verification

1. Set `Wsi:UseMockWorker=false` in API.
2. Trigger analysis: `POST /api/v1/wsi/uploads/{id}/analyze`.
3. Job moves to `Processing` (saga receives event).
4. Worker consumes, runs inference, publishes completion.
5. Job moves to `Completed` (saga receives `WsiAnalysisCompletedEvent`).
6. Poll `GET /api/v1/wsi/jobs/{id}` to confirm.

---

## References

- [SCALABLE-GPU-INFERENCE.md](SCALABLE-GPU-INFERENCE.md) – Scalability, cost, large images
- [SYSTEM-ARCHITECTURE.md](SYSTEM-ARCHITECTURE.md) – Implementation status
- [high_level_platform.md](high_level_platform.md) – Architecture, EKS GPU
