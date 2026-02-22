# 1.2 Future Functionalities

## ✅ Implemented (Beyond Original MVP)

- **SSO** – Google and Microsoft OAuth; disabled in development; token via URL fragment
- **Serilog** – Structured logging with request logging
- **Health Probes** – `/health`, `/health/ready`, `/health/live` for Docker/K8s
- **Error Boundaries** – React error boundaries with fallback UI
- **API Docs** – Swagger descriptions and ProducesResponseType
- **Rate Limiting** – Auth endpoints (login, register)
- **CORS** – Configurable origins; restricted in production
- **Secrets** – Production config via environment variables
- **Stripe Payments** – Payment entity, PaymentIntent creation, webhook for `payment_intent.succeeded`
- **Recurring Orders** – RecurringOrderTemplate entity, Hangfire cron job for automated round creation
- **Notifications** – Email reminders before deadlines (background job every 15 min)
- **Microsoft Teams Bot** – Bot Framework webhook, list rounds, link account via code
- **Event-driven** – MassTransit; production: AWS SNS/SQS (pub/sub); development: RabbitMQ; tests: InMemory. Apache 2.0 only.
- **global.json** – SDK version pinning
- **JsonPropertyName** – camelCase on all DTOs
- **Technical debt resolved** – Hangfire PostgreSqlStorage, CloudAdapter, Stripe webhook model binding, MassTransit refactor

---

## 🧩 Feature Roadmap

### 1️⃣ Microsoft Teams Integration ✅

Implemented: Bot Framework webhook, `/api/v1/bot` endpoint, list rounds, link account via code.

### 2️⃣ Payment Handling ✅

Implemented: Stripe PaymentIntent, webhook, Payment entity.

### 3️⃣ Recurring Orders ✅

Implemented: RecurringOrderTemplate CRUD, Hangfire job for cron-based round creation.

### 4️⃣ Multi-Tenant Support ✅

Implemented: Tenant entity, TenantId on all entities, tenant-aware authorization, Admin role, `GET /api/v1/admin/tenants`. Optional: row-level security in PostgreSQL.

---

### 5️⃣ Notifications ✅

Implemented: Email reminders before deadline (configurable minutes). In-app alerts via `GET /api/v1/notifications/unread` and `POST /api/v1/notifications/{id}/read`. DeadlineReminderJob creates both email and in-app notifications.

Remaining: Push notifications (web push). See `.cursor/plans/notifications_push_inapp.plan.md`.