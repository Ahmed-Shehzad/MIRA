# HIVE Food Ordering Coordination System (MVP)

A lightweight web-based system for coordinating food orders between companies located in HIVE Göppingen.

The system is designed to be simple, production-ready, and cloud-extensible.

---

## 🚀 Tech Stack

### Frontend
- React (TypeScript)
- Vite
- React Router
- TailwindCSS
- React Query (TanStack Query) + Axios

### Backend
- ASP.NET Core (.NET 10 Web API)
- Entity Framework Core
- ASP.NET Identity
- JWT Authentication
- MassTransit (event-driven: AWS SNS/SQS in production, RabbitMQ in dev)
- Hangfire (background jobs in PostgreSQL)

### Database
- PostgreSQL (development and production)

### Infrastructure
- Docker & Docker Compose (backend, frontend, PostgreSQL, RabbitMQ for dev, MailHog)

### Email
- MailHog (development)
- Amazon SES (production)

---

## 🧱 Architecture Overview

React SPA (TypeScript)
        ↓
ASP.NET Core Web API (.NET 10)
        ↓
Entity Framework Core
        ↓
PostgreSQL

---

## ⚙️ Setup Instructions

### Option A: Docker (Recommended)

Requirements:
- Docker and Docker Compose

Run:

    docker compose up --build

Services:
- Frontend: http://localhost:5173
- Backend API: http://localhost:5000
- Swagger UI: http://localhost:5000/swagger (development only)
- Hangfire Dashboard: http://localhost:5000/hangfire (development only)
- RabbitMQ Management: http://localhost:15672 (guest/guest)
- PostgreSQL: localhost:5432
- MailHog UI (email): http://localhost:8025

---

### Option B: Local Development

#### Backend

Requirements:
- .NET 10 SDK (or .NET 8+)
- PostgreSQL

Run:

    cd backend
    dotnet restore
    dotnet ef database update
    dotnet run

Backend runs on:
    http://localhost:5000

#### Frontend

Requirements:
- Node.js 18+

Run:

    cd frontend
    npm install
    npm run dev

Frontend runs on:
    http://localhost:5173

---

## 🧪 Testing

    dotnet test HiveOrders.slnx

- **Unit tests** (HiveOrders.Api.Tests): OrderRoundHandler, JwtTokenService
- **Integration tests** (HiveOrders.Api.IntegrationTests): Auth (register, login, me, duplicate), Health, OrderRounds (CRUD, authenticated and unauthenticated)

## 🔍 Code Quality

- **SonarAnalyzer** (C#): Runs on build; rules configured in `.editorconfig`
- **SonarQube/SonarCloud**: `.github/workflows/sonarcloud.yml` runs on push to `main`/`master`. Add `SONAR_TOKEN` and `SONAR_ORGANIZATION` to GitHub secrets to enable.

---

## 👤 How to Use

1. Register using company email
2. Confirm email (check MailHog at http://localhost:8025 in dev)
3. Create an Order Round
4. Share link with colleagues
5. Add items before deadline
6. Close order and export summary

---

## 🔐 Authentication

- Email verification required
- JWT-based authentication
- Role-based authorization (User / Manager)
- **SSO** (production only): Google and Microsoft OAuth; disabled in development; token passed via URL fragment

---

## 🔧 SSO Configuration (Production)

SSO is disabled in development. To enable Google or Microsoft SSO in production:

**Google:** Create OAuth credentials in [Google Cloud Console](https://console.cloud.google.com/apis/credentials). Redirect URI: `https://your-api-host/signin-google`.

**Microsoft:** Create app in [Azure Portal](https://portal.azure.com/) (App registrations). Redirect URI: `https://your-api-host/signin-microsoft`.

Set in `appsettings.Production.json` or environment variables:

```json
{
  "Authentication": {
    "Google": { "ClientId": "...", "ClientSecret": "...", "FrontendRedirectUri": "https://your-frontend/login" },
    "Microsoft": { "ClientId": "...", "ClientSecret": "...", "FrontendRedirectUri": "https://your-frontend/login" }
  }
}
```

Check available providers: `GET /api/v1/auth/sso/providers`

---

## 🏥 Health Probes

- `/health` – Full health check (DB)
- `/health/ready` – Readiness (DB connectivity; use for K8s/Docker)
- `/health/live` – Liveness (simple ping)

## 🔐 Production Configuration

See [docs/production-config.md](docs/production-config.md) for environment variables and secrets. CORS origins must be explicitly set in production.

## ☁️ AWS Deployment Plan

- Backend → ECS (Fargate) or EKS (containerized)
- Frontend → S3 + CloudFront (static) or ECS/EKS (containerized)
- Database → Amazon RDS for PostgreSQL
- Email → Amazon SES
- Secrets → AWS Secrets Manager