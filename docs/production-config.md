# Production Configuration

Use environment variables or a secrets manager for production. Never commit secrets to source control.

## Required Environment Variables

| Variable | Description |
|----------|-------------|
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string |
| `Jwt__Key` | JWT signing key (min 32 chars) |
| `Cors__AllowedOrigins__0` | Allowed frontend origin (e.g. `https://app.example.com`) |
| `Cors__AllowedOrigins__1` | Additional origin if needed |

## Optional (SSO)

| Variable | Description |
|----------|-------------|
| `Authentication__Google__ClientId` | Google OAuth client ID |
| `Authentication__Google__ClientSecret` | Google OAuth client secret |
| `Authentication__Google__FrontendRedirectUri` | Frontend URL for SSO callback |
| `Authentication__Microsoft__ClientId` | Microsoft OAuth client ID |
| `Authentication__Microsoft__ClientSecret` | Microsoft OAuth client secret |
| `Authentication__Microsoft__FrontendRedirectUri` | Frontend URL for SSO callback |

## Messaging – Event-Driven (Pub/Sub)

Transport selection (in order of precedence):

1. **AWS SNS + SQS** (production) – when `AWS__Region` is set
2. **RabbitMQ** (local Docker) – when `RabbitMQ__Host` is set
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

### Development: RabbitMQ

| Variable | Description |
|----------|-------------|
| `RabbitMQ__Host` | RabbitMQ host (e.g. `rabbitmq` in Docker) |
| `RabbitMQ__Username` | RabbitMQ username |
| `RabbitMQ__Password` | RabbitMQ password |

## Optional (Email)

| Variable | Description |
|----------|-------------|
| `Email__SmtpHost` | SMTP host (e.g. Amazon SES) |
| `Email__SmtpPort` | SMTP port |
| `Email__FromAddress` | Sender email |

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
- **JWT** – Includes `tenant_id` claim for tenant-aware authorization.
- **Admin** – `Admin` role can access `/api/v1/admin/tenants`. Assign via `UserManager.AddToRoleAsync(user, "Admin")`.
- **Row-level security** – Optional: enable PostgreSQL RLS with `current_setting('app.tenant_id')` for stricter isolation.

## Rate Limiting

Auth endpoints (login, register, SSO) are rate limited. Configure via:

| Variable | Default | Description |
|----------|---------|-------------|
| `RateLimiting__Auth__PermitLimit` | 20 | Requests per window |
| `RateLimiting__Auth__WindowMinutes` | 1 | Window duration |
