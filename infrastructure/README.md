# MIRA Infrastructure (Pulumi + AWS)

TypeScript-based Infrastructure as Code for deploying MIRA across dev, staging, and production.

## Prerequisites

- [Node.js](https://nodejs.org/) 18+
- [Pulumi CLI](https://www.pulumi.com/docs/install/)
- AWS CLI configured with credentials

## Setup

```bash
cd infrastructure
npm install
# If using WebSocket API (enableWebSocketApi=true), install Lambda deps:
cd lambda/ws-connect && npm install && cd ../..
```

## Stacks

| Stack    | Environment | Use Case                    |
|----------|-------------|-----------------------------|
| `dev`    | dev         | Local development, testing  |
| `staging`| staging     | Pre-production validation   |
| `prod`   | prod        | Production                  |

## Configuration

### Required (per stack)

- `dbPassword` – RDS PostgreSQL password (use a secret):

  ```bash
  pulumi config set dbPassword <your-password> --secret
  ```

### Optional (defaults in `config.ts`)

- `awsRegion` – AWS region (default: `us-east-1`)
- `projectName` – Resource name prefix (default: `mira`)
- `dbInstanceClass` – RDS instance class (default: `db.t3.micro`)
- `dbAllocatedStorage` – RDS storage in GB (default: `20`)
- `ecsCpu` – ECS task CPU units (default: `256`)
- `ecsMemory` – ECS task memory in MB (default: `512`)
- `desiredCount` – ECS service desired count (default: `1`)
- `acmCertificateArn` – ACM certificate ARN for ALB HTTPS (when set, HTTP redirects to HTTPS)
- `enableEksGpu` – Create EKS cluster with GPU node group (default: `false`)
- `enableWebSocketApi` – Create API Gateway WebSocket API + DynamoDB + Lambda (default: `false`)
- `enableWsiWorker` – Create ECS Fargate service for WSI worker (default: `true`)
- `domainName` – Root domain (e.g. `example.com`; required with `hostedZoneId` for custom domain)
- `hostedZoneId` – Route53 hosted zone ID (for ACM DNS validation and API A record)
- `apiSubdomain` – Subdomain for API (default: `api`; e.g. api.example.com)
- `frontendSubdomain` – Subdomain for frontend (e.g. `app` → app.example.com; requires `domainName` + `hostedZoneId`)
- `enableXRayTracing` – Enable AWS X-Ray tracing for backend (default: `false`)
- `apiScalingMin`, `apiScalingMax` – ECS API auto-scaling (CPU target 70%); when both set, enables scaling
- `workerScalingMin`, `workerScalingMax` – ECS WSI worker auto-scaling (CPU target 70%); when both set, enables scaling

## Commands

```bash
# Select stack
pulumi stack select dev

# Preview changes
pulumi preview

# Deploy
pulumi up

# View outputs (ECR URL, ALB, CloudFront, etc.)
pulumi stack output
```

## Resources Created

- **VPC** – 2 AZs, single NAT gateway
- **RDS PostgreSQL** – Private subnets, SSL required
- **S3** – Uploads bucket, frontend bucket, CloudFront logs bucket
- **SQS** – Job queue + DLQ (maxReceiveCount: 5) for MassTransit
- **Cognito** – User pool, app client, hosted domain
- **ECR** – API and WSI worker image repositories
- **ECS Fargate** – API service, optional WSI worker service, behind ALB
- **CloudFront** – Frontend CDN (S3 origin) with access logging
- **CloudWatch** – Log groups for ECS, DLQ depth alarm
- **Secrets Manager** – DB connection string for ECS
- **Optional:** EKS GPU cluster (see `k8s/` for worker manifests), API Gateway WebSocket + DynamoDB + Lambda

## Post-Deploy

**CI/CD:** When GitHub Actions secrets are configured, push to `main` triggers `.github/workflows/deploy.yml` which builds, pushes to ECR (`mira-api`, `mira-wsi-worker`), updates ECS, syncs frontend to S3, and invalidates CloudFront.

**Manual steps** (when not using CI/CD):

1. Build and push images to ECR:
   ```bash
   ECR_URL=$(pulumi stack output ecrRepositoryUrl)
   aws ecr get-login-password --region <region> | docker login --username AWS --password-stdin $ECR_URL
   docker build -t $ECR_URL:latest -f backend/Dockerfile .
   docker push $ECR_URL:latest
   # Worker: use ecrWorkerRepositoryUrl
   ```

2. Deploy frontend to S3:
   ```bash
   npm run build --workspace=frontend
   aws s3 sync frontend/dist s3://$(pulumi stack output frontendBucketName) --delete
   ```

3. Invalidate CloudFront cache (if needed):
   ```bash
   aws cloudfront create-invalidation --distribution-id $(pulumi stack output cloudFrontDistributionId) --paths "/*"
   ```
