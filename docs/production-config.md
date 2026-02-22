# Production Configuration

Production follows [high_level_platform.md](high_level_platform.md) and uses fully managed AWS infrastructure. Use environment variables or AWS Secrets Manager. Never commit secrets to source control.

## Production Architecture (high_level_platform.md)

| Component | AWS Service | Config Section |
|-----------|-------------|----------------|
| Auth | Cognito | `AWS:Cognito` |
| Storage | S3 | `AWS:S3` |
| Metadata | RDS PostgreSQL | `ConnectionStrings` |
| Job Queue | SQS | `AWS` (MassTransit) |
| Compute | EKS / ECS | Deployment |
| Real-time | SignalR (current) / WebSocket API / AppSync | See production-architecture.md |
| CDN | CloudFront | Deployment |
| Tracing | AWS X-Ray | `Tracing:XRay` (when `enableXRayTracing=true`) |

## Required Environment Variables

| Variable | Description |
|----------|-------------|
| `ConnectionStrings__DefaultConnection` | RDS PostgreSQL connection string |
| `Cors__AllowedOrigins__0` | Allowed frontend origin (e.g. `https://app.example.com`) |
| `Cors__AllowedOrigins__1` | Additional origin if needed |

## Auth: AWS Cognito (Required)

**ASP.NET Identity has been removed.** Auth uses Cognito only. Configure a Cognito User Pool and set:

| Variable | Description |
|----------|-------------|
| `AWS__Cognito__UserPoolId` | Cognito User Pool ID (required) |
| `AWS__Cognito__Region` | AWS region (required) |
| `AWS__Cognito__ClientId` | App client ID (for audience validation) |

SSO (Google, Microsoft) is configured as Cognito Identity Providers; use Cognito Hosted UI.

### Backend

| Variable | Description |
|----------|-------------|
| `AWS__Cognito__UserPoolId` | Cognito User Pool ID |
| `AWS__Cognito__Region` | AWS region (or use `AWS__Region`) |
| `AWS__Cognito__ClientId` | App client ID (for audience validation) |

### Frontend (Cognito Hosted UI)

When `VITE_USE_COGNITO=true`, the login page shows "Sign in with Cognito" and redirects to Cognito Hosted UI.

| Variable | Description |
|----------|-------------|
| `VITE_USE_COGNITO` | Set to `true` to enable Cognito login |
| `VITE_COGNITO_USER_POOL_ID` | Cognito User Pool ID |
| `VITE_COGNITO_CLIENT_ID` | App client ID |
| `VITE_COGNITO_DOMAIN` | Cognito domain (e.g. `hive-orders.auth.us-east-1.amazoncognito.com`) |
| `VITE_COGNITO_REGION` | AWS region (optional, default `us-east-1`) |

Configure the Cognito App Client with callback URL: `https://<your-frontend>/login` (implicit grant for `id_token`).

## Messaging – Event-Driven (Pub/Sub)

Transport selection (in order of precedence):

1. **AWS SNS + SQS** (production) – when `AWS__Region` is set
2. **LocalStack** (local Docker) – when `AWS__ServiceUrl` is set
3. **InMemory** (tests) – when neither is configured

### Production: AWS SNS + SQS

Uses **MassTransit.AmazonSQS** and **AWS SDK** (both Apache 2.0, open source). MassTransit publishes to SNS topics and consumes from SQS queues; SNS fans out to SQS subscriptions (pub/sub). No commercial or paid libraries.

| Variable | Description |
|----------|-------------|
| `AWS__Region` | AWS region (e.g. `us-east-1`) |
| `AWS__AccessKey` | IAM access key (optional; use IAM role or `AWS_ACCESS_KEY_ID` env) |
| `AWS__SecretKey` | IAM secret key (optional; use IAM role or `AWS_SECRET_ACCESS_KEY` env) |
| `AWS__Scope` | Optional prefix for queue/topic names (e.g. `hive-orders`) |

**IAM permissions** required: `sqs:*`, `sns:*` on the relevant resources. Prefer IAM roles (ECS task role, EC2 instance profile) over access keys.

## S3 (Presigned URLs)

For file uploads per high_level_platform.md:

| Variable | Description |
|----------|-------------|
| `AWS__S3__BucketName` | S3 bucket for uploads |
| `AWS__S3__Region` | Bucket region (or use `AWS__Region`) |

API: `POST /api/v1/storage/upload-url` (general uploads) and `POST /api/v1/wsi/upload-url` (WSI-specific) return presigned PUT URLs.

### Development: LocalStack (SQS/SNS)

| Variable | Description |
|----------|-------------|
| `AWS__Region` | Region (e.g. `us-east-1`) |
| `AWS__ServiceUrl` | LocalStack endpoint (e.g. `http://localstack:4566` in Docker) |
| `AWS__AccessKey` | Access key (e.g. `test`) |
| `AWS__SecretKey` | Secret key (e.g. `test`) |

## Optional (Email)

| Variable | Description |
|----------|-------------|
| `Email__SmtpHost` | SMTP host (e.g. Amazon SES) |
| `Email__SmtpPort` | SMTP port |
| `Email__FromAddress` | Sender email |

## Error Reporting (Development)

| Variable | Description |
|----------|-------------|
| `ErrorReporting__DevEmailAddress` | Email address for exception reports (development) |

## Real-time (WebSocket migration)

**Current:** SignalR at `/hubs/notifications`. Enable sticky sessions on ALB for WebSocket.

**API Gateway WebSocket** (when `enableWebSocketApi=true` in Pulumi):

| Variable | Description |
|----------|-------------|
| `Realtime__WebSocketEndpoint` | HTTPS endpoint for PostToConnection (e.g. `https://xxx.execute-api.region.amazonaws.com/dev`) |
| `Realtime__ConnectionsTableName` | DynamoDB table name for connection IDs (Pulumi output `webSocketConnectionsTableName`) |
| `VITE_WEBSOCKET_URL` | Frontend: WebSocket URL (e.g. `wss://xxx.execute-api.region.amazonaws.com/dev`); when set, frontend uses API Gateway instead of SignalR |

Backend pushes to both SignalR and API Gateway when configured. See [production-architecture.md](production-architecture.md#websocket--appsync).

## Docker / Kubernetes

Set via `environment` in docker-compose or `env` in K8s Deployment. For K8s, prefer Secrets and ConfigMaps.

## CORS

In production, **Cors:AllowedOrigins** must be explicitly set via environment variables. The app will not start if origins are empty in Production.

**Example (env vars):**
```bash
Cors__AllowedOrigins__0=https://app.example.com
Cors__AllowedOrigins__1=https://admin.example.com
```

**Example (appsettings.Production.json):**
```json
{
  "Cors": {
    "AllowedOrigins": ["https://app.example.com"]
  }
}
```

## HTTP Clients (Refit) – Resilience

Refit clients use `Microsoft.Extensions.Http.Resilience` with `AddStandardResilienceHandler`. Configure via `HttpClients:{ClientName}:Resilience` in appsettings.json.

| Path | Default | Description |
|------|---------|-------------|
| `AttemptTimeout.Timeout` | 00:00:10 | Per-attempt timeout (TimeSpan) |
| `TotalRequestTimeout.Timeout` | 00:00:30 | Total timeout including retries |
| `Retry.MaxRetryAttempts` | 3 | Max retries on transient errors |
| `Retry.BackoffType` | Exponential | `Exponential` or `Linear` |
| `Retry.UseJitter` | true | Add jitter to retry delays |
| `Retry.Delay` | 00:00:02 | Base delay between retries |
| `CircuitBreaker.FailureRatio` | 0.1 | 10% failure ratio triggers open |
| `CircuitBreaker.MinimumThroughput` | 100 | Min requests before sampling |
| `CircuitBreaker.SamplingDuration` | 00:00:30 | Sampling window |
| `CircuitBreaker.BreakDuration` | 00:00:05 | Duration circuit stays open |

**Example:** `HttpClients:ExternalService:Resilience` in appsettings.json. Omit `RateLimiter` (API changed); defaults apply.

## Microsoft Teams Bot (CloudAdapter)

The Teams bot uses **CloudAdapter** (Bot Framework v4). Configure via `Teams` section:

| Variable | Description |
|----------|-------------|
| `Teams__MicrosoftAppId` | Microsoft App Registration ID |
| `Teams__MicrosoftAppPassword` | Microsoft App Registration secret |

**Example (appsettings.json):**
```json
{
  "Teams": {
    "MicrosoftAppId": "your-app-id",
    "MicrosoftAppPassword": "your-app-password"
  }
}
```

## Multi-Tenant

- **TenantId** – All tenant-scoped entities (OrderRound, Payment, RecurringOrderTemplate, etc.) include TenantId.
- **Cognito id_token** – Includes `tenant_id` claim (set by CognitoUserProvisioningMiddleware) for tenant-aware authorization.
- **Admins** – Users in the `Admins` Cognito group can access `/api/v1/admin/*`. Assign via Cognito AdminAddUserToGroup or `POST /api/v1/admin/users/{userId}/assign-admin` (existing admin only).
- **Row-level security** – Optional: enable PostgreSQL RLS with `current_setting('app.tenant_id')` for stricter isolation.

## Stripe (Payments)

| Variable | Description |
|----------|-------------|
| `Stripe__SecretKey` | Stripe secret key (required for payment intents) |
| `Stripe__WebhookSecret` | Webhook signing secret (required for `POST /api/v1/webhooks/stripe`) |
| `Stripe__PublishableKey` | Publishable key (frontend; optional for backend) |

API: `POST /api/v1/order-rounds/{orderRoundId}/payments` creates a PaymentIntent. Webhook at `POST /api/v1/webhooks/stripe` handles `payment_intent.succeeded`.

### Webhook URL Verification

After deployment, configure the Stripe webhook in the [Stripe Dashboard](https://dashboard.stripe.com/webhooks):

1. **Endpoint URL** – Must match the deployed API base URL plus path: `https://<api-host>/api/v1/webhooks/stripe` (e.g. `https://api.example.com/api/v1/webhooks/stripe`).
2. **Events** – Subscribe to `payment_intent.succeeded` (and optionally `payment_intent.payment_failed`).
3. **Signing secret** – Copy the webhook signing secret and set `Stripe__WebhookSecret` in production config.

If the route or version changes, update the endpoint URL in the Stripe Dashboard to avoid 404s.

## Push Notifications (Web Push)

| Variable | Description |
|----------|-------------|
| `Push__VapidPublicKey` | VAPID public key for push subscription |
| `Push__VapidPrivateKey` | VAPID private key (server-side; never expose to client) |

API: `GET /api/v1/notifications/push/vapid-public-key` returns the public key. `POST /api/v1/notifications/push/subscribe` registers a push subscription.

## Tracing (AWS X-Ray)

When `enableXRayTracing=true` in Pulumi config, the backend sends traces to AWS X-Ray. Configure via environment or appsettings:

| Variable | Description |
|----------|-------------|
| `Tracing__XRay__Enabled` | Set to `true` to enable X-Ray tracing (ECS task gets this when `enableXRayTracing=true`) |
| `Tracing__XRay__ServiceName` | Service name in X-Ray (default: `mira-api`) |

**Pulumi:** Set `enableXRayTracing: true` in `Pulumi.*.yaml` or via `pulumi config set enableXRayTracing true`. The ECS task role receives `xray:PutTraceSegments` and `xray:PutTelemetryRecords` permissions.

## WSI (Whole Slide Image)

| Variable | Description |
|----------|-------------|
| `Wsi__UseMockWorker` | Set to `true` in dev to simulate GPU worker (jobs complete automatically). **Never enable in production** – use real GPU worker. |

## Testing (Development Only)

| Variable | Description |
|----------|-------------|
| `Testing__UseLocalJwt` | Set to `true` to enable `POST /api/v1/auth/test-token` (provisions test user, returns JWT) |
| `Testing__SkipRateLimiting` | Set to `true` to disable auth rate limiting |

**Security:** Never enable these in production.

## Rate Limiting

Auth endpoints are rate limited. Configure via:

| Variable | Default | Description |
|----------|---------|-------------|
| `RateLimiting__Auth__PermitLimit` | 20 | Requests per window |
| `RateLimiting__Auth__WindowMinutes` | 1 | Window duration |
