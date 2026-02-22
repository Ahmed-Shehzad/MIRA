# MIRA – HIVE Food Ordering Coordination System

A lightweight web-based system for coordinating food orders between companies located in HIVE Göppingen, with extended support for Whole Slide Image (WSI) upload and analysis.

**Scope:** (1) Food ordering – order rounds, items, payments, recurring orders, Teams bot, notifications. (2) WSI platform – upload, analysis jobs, viewer. Both share the same backend, database, auth, and infrastructure.

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
- AWS Cognito (auth for all environments)
- JWT (Cognito id_token)
- MassTransit (event-driven: AWS SNS/SQS in production, LocalStack in dev)
- Hangfire (background jobs in PostgreSQL)

### Database
- PostgreSQL (development and production)

### Infrastructure
- Docker & Docker Compose (backend, frontend, PostgreSQL, LocalStack for dev, MailHog)

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
- LocalStack (SQS/SNS): http://localhost:4566
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

1. Sign in with Cognito (Hosted UI)
2. Create an Order Round
3. Share link with colleagues
4. Add items before deadline
5. Close order and export summary

---

## 🔐 Authentication

- **AWS Cognito** for all environments
- JWT-based (Cognito id_token)
- Group-based authorization (Admins, Managers, Users)
- SSO via Cognito Identity Providers (Google, Microsoft) – configure in Cognito User Pool

See [docs/production-config.md](docs/production-config.md) for Cognito configuration.

---

## 🏥 Health Probes

- `/health` – Full health check (DB)
- `/health/ready` – Readiness (DB connectivity; use for K8s/Docker)
- `/health/live` – Liveness (simple ping)

## 🔐 Production Configuration

See [docs/production-config.md](docs/production-config.md) for environment variables and secrets. CORS origins must be explicitly set in production.

## ☁️ Production Architecture

Production follows [docs/high_level_platform.md](docs/high_level_platform.md) strictly. Fully managed AWS infrastructure:

| Component | AWS Service |
|-----------|-------------|
| Auth | **Cognito** (all environments) |
| Storage | **S3** (presigned URLs) |
| Metadata | **RDS PostgreSQL** |
| Job Queue | **SQS** (via MassTransit) |
| Compute | **EKS** or ECS Fargate |
| Real-time | WebSocket API or AppSync |
| CDN | **CloudFront** |
| Email | Amazon SES |

See [docs/production-architecture.md](docs/production-architecture.md) and [docs/production-config.md](docs/production-config.md).