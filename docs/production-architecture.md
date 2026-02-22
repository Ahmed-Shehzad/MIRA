# Production Architecture

**Production follows [high_level_platform.md](high_level_platform.md) strictly.** This document maps the AWS-managed architecture to the production deployment.

---

## Architecture Reference

Production uses fully managed AWS infrastructure for scalability, security, and compliance:

| Component | AWS Service | Purpose |
|-----------|-------------|---------|
| Auth | **Cognito** | User pools, MFA, compliance; Cognito for all environments |
| Storage | **S3** | Large binaries, presigned URLs for uploads |
| Metadata | **RDS PostgreSQL** | Relational data, jobs, users, audit; row-level security |
| Job Queue | **SQS** | Decouples API from workers; retries; backpressure |
| Compute | **EKS** (or ECS Fargate) | API and workers; GPU nodes for inference workloads |
| Real-time | **AppSync** or **API Gateway WebSocket** | Push job status to frontend |
| CDN | **CloudFront** | Static assets, tile delivery |
| Monitoring | **CloudWatch**, **X-Ray** | Logs, metrics, tracing |

---

## System Flow (Production)

1. User logs in via **Cognito**
2. Upload files via **presigned S3 URL** (when applicable)
3. Metadata stored in **RDS PostgreSQL**
4. API publishes job to **SQS**
5. Worker (EKS/ECS) processes job
6. Results stored in **S3**
7. Job status updated in **RDS**
8. **WebSocket/AppSync** notifies frontend

---

## Environment Modes

| Mode | Auth | Messaging | Storage |
|------|------|-----------|---------|
| **Development** | AWS Cognito | LocalStack or InMemory | Local / Docker |
| **Staging** | AWS Cognito | SQS | PostgreSQL, S3 optional |
| **Production** | AWS Cognito | AWS SNS + SQS | S3, RDS |

---

## Deployment Topology

```
                    ┌─────────────────┐
                    │   CloudFront    │
                    │   (CDN)         │
                    └────────┬────────┘
                             │
         ┌───────────────────┼───────────────────┐
         │                   │                   │
         ▼                   ▼                   ▼
┌─────────────────┐ ┌───────────────┐ ┌─────────────────┐
│  React SPA      │ │  Cognito      │ │  ALB             │
│  (S3 + CF)      │ │  (Auth)       │ │  (API)           │
└─────────────────┘ └───────────────┘ └────────┬────────┘
                                               │
                    ┌──────────────────────────┼──────────────────────────┐
                    │                          │                          │
                    ▼                          ▼                          ▼
             ┌─────────────┐            ┌───────────┐            ┌─────────────┐
             │ RDS         │            │ SQS       │            │ S3           │
             │ PostgreSQL  │            │ (Jobs)    │            │ (Storage)    │
             └─────────────┘            └─────┬─────┘            └─────────────┘
                    ▲                          │
                    │                          ▼
                    │                   ┌─────────────┐
                    └───────────────────│ EKS / ECS  │
                                        │ (Workers)  │
                                        └─────────────┘
```

---

## Deploy Workflow (GitHub Actions)

`.github/workflows/deploy.yml` builds and deploys on push to `main` or manual dispatch.

| Secret | Required | Description |
|--------|----------|-------------|
| `AWS_ROLE_ARN` | Yes | IAM role for OIDC (or use access keys) |
| `ECR_REGISTRY` | Yes | ECR registry URL (e.g. `123456789.dkr.ecr.us-east-1.amazonaws.com`) |
| `ECS_CLUSTER` | No | ECS cluster name (enables backend deploy) |
| `ECS_SERVICE` | No | ECS service name |
| `ECS_WORKER_CLUSTER` | No | ECS cluster for WSI worker |
| `ECS_WORKER_SERVICE` | No | ECS service for WSI worker |
| `S3_BUCKET_FRONTEND` | No | S3 bucket for frontend static files |
| `CLOUDFRONT_DISTRIBUTION_ID` | No | CloudFront distribution ID (invalidates cache after S3 sync) |

**ECR repos:** Workflow pushes to `mira-api` and `mira-wsi-worker` (match Pulumi `projectName: mira`).

**Note:** ECS task definition must reference the ECR image (e.g. `:latest`). The workflow pushes the image and runs `aws ecs update-service --force-new-deployment`.

### Infrastructure (Pulumi)

`.github/workflows/deploy-infra.yml` runs `pulumi preview` on PRs when `infrastructure/**` changes, and supports manual `pulumi up` via workflow_dispatch. Requires `PULUMI_ACCESS_TOKEN` and `AWS_ROLE_ARN`. See [infrastructure/README.md](../infrastructure/README.md).

### EKS Worker Deploy

When `enableEksGpu=true`, the WSI worker runs on EKS. CI/CD does not auto-deploy EKS manifests. After `pulumi up`, manually:

```bash
aws eks update-kubeconfig --name $(pulumi stack output eksClusterName) --region $AWS_REGION
kubectl annotate serviceaccount wsi-worker -n mira eks.amazonaws.com/role-arn=$(pulumi stack output eksWsiWorkerRoleArn) --overwrite
envsubst < infrastructure/k8s/wsi-worker-deployment.yaml | kubectl apply -f -
```

See [infrastructure/k8s/README.md](../infrastructure/k8s/README.md).

---

## Cognito Configuration

Production uses Cognito User Pools. Configure:

| Variable | Description |
|----------|-------------|
| `AWS__Cognito__UserPoolId` | Cognito User Pool ID (e.g. `us-east-1_xxxxx`) |
| `AWS__Cognito__Region` | AWS region for Cognito |
| `AWS__Cognito__ClientId` | App client ID (for audience validation) |

When these are set, JWT validation uses Cognito JWKS. Cognito is required for all environments.

---

## S3 Configuration

For presigned URL uploads:

| Variable | Description |
|----------|-------------|
| `AWS__S3__BucketName` | S3 bucket for uploads |
| `AWS__S3__Region` | Bucket region |
| `AWS__S3__PresignedUrlExpirationMinutes` | URL validity (default 15) |

---

## Auto-Scaling (ECS)

When `apiScalingMin` and `apiScalingMax` (or `workerScalingMin` and `workerScalingMax`) are set in Pulumi, Application Auto Scaling is enabled with CPU target tracking (70%). For EKS, use KEDA with SQS-based scaling (future enhancement).

## WebSocket / AppSync

**Current:** SignalR (`/hubs/notifications`) is used in all environments for real-time notifications.

**Production migration path** (per high_level_platform.md):

- **Option A:** API Gateway WebSocket API – connect from frontend, receive push from backend (implemented when `enableWebSocketApi=true`)
- **Option B:** AWS AppSync (GraphQL) – subscriptions for job status (future option)

### Migration steps (API Gateway WebSocket)

1. Enable in Pulumi: `pulumi config set enableWebSocketApi true`.
2. Deploy; outputs `webSocketUrl` (e.g. `wss://xxx.execute-api.region.amazonaws.com/dev`).
3. Add a Lambda or backend service that calls API Gateway Management API to push messages to connected clients.
4. Configure `Realtime__WebSocketUrl` and `VITE_WEBSOCKET_URL` with the output URL.

Until migration, SignalR works behind ALB (ensure sticky sessions are enabled for WebSocket).

---

## Custom Domains

When `domainName` and `hostedZoneId` are set in Pulumi:

- **API:** Route53 A record for `apiSubdomain.domainName` (default `api`) → ALB. ACM cert created or use `acmCertificateArn`.
- **Frontend:** When `frontendSubdomain` is set (e.g. `app`), Route53 A record for `app.domainName` → CloudFront. Reuses the same ACM cert (`*.domainName`).

**CloudFront ACM requirement:** CloudFront requires ACM certificates to be in **us-east-1**. Deploy the stack in `us-east-1` (default) when using frontend custom domain, or create the cert in us-east-1 separately and pass `acmCertificateArn`.

---

## Tracing (X-Ray)

When `enableXRayTracing=true` in Pulumi, the backend sends traces to AWS X-Ray. The ECS task role receives `xray:PutTraceSegments` and `xray:PutTelemetryRecords`. Configure `Tracing__XRay__Enabled` and `Tracing__XRay__ServiceName` via environment. See [production-config.md](production-config.md#tracing-aws-x-ray).

---

## EKS IRSA (Pod-Level IAM)

When `enableEksGpu=true`, Pulumi creates an OIDC provider for the EKS cluster and an IAM role for the `mira:wsi-worker` service account. Annotate the service account after deploy:

```bash
kubectl annotate serviceaccount wsi-worker -n mira eks.amazonaws.com/role-arn=$(pulumi stack output eksWsiWorkerRoleArn) --overwrite
```

Pods then use this role for S3/SQS instead of the node role. See [infrastructure/k8s/README.md](../infrastructure/k8s/README.md).

---

## Compliance

- Use managed AWS services for security and compliance (HIPAA, MDR/IVDR when applicable)
- Secrets in AWS Secrets Manager
- VPC isolation for RDS and EKS
- CloudTrail for audit logging
