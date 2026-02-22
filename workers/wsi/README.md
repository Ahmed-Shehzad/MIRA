# WSI Worker

Production worker for WSI (Whole Slide Image) analysis. Consumes `WsiAnalysisRequestedEvent` from SQS, runs inference, and publishes `WsiAnalysisCompletedEvent` or `WsiAnalysisFailedEvent`.

## Configuration

| Variable | Description |
|----------|-------------|
| `AWS__Region` | AWS region (required for SQS) |
| `AWS__Scope` | MassTransit queue scope (e.g. `hive-orders`) – must match API |
| `AWS__ServiceUrl` | LocalStack endpoint for dev |
| `Wsi__InferenceServiceUrl` | Optional HTTP endpoint for inference. If set, worker POSTs to `/analyze`. If empty, simulates completion. |

## Inference Modes

1. **Simulated** (default): No `InferenceServiceUrl` – worker delays 1s and publishes a placeholder result.
2. **HTTP service**: Set `Wsi:InferenceServiceUrl` – worker POSTs `{ jobId, uploadId, tenantId, s3Key }` to `{url}/analyze`, expects `{ resultS3Key }`.
3. **Extend**: Override `RunInferenceAsync` in `WsiAnalysisWorkerConsumer` to call GPU service, Python, etc.

## Run

```bash
cd workers/wsi
dotnet run
```

## Deploy

Build and push to ECR, then deploy to ECS Fargate (or EKS). Use same VPC, SQS, and IAM as the API. See [docs/WSI-GPU-WORKER.md](../../docs/WSI-GPU-WORKER.md).
