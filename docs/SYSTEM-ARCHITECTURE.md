# System Architecture

Cloud-native platform for Whole Slide Image (WSI) analysis on AWS. This document describes the main components, their interactions, tool choices, and rationale.

---

## 1. Main Architectural Components

| # | Component | Responsibility |
|---|-----------|---------------|
| 1 | **Client** | React SPA, WSI viewer (OpenSeadragon) |
| 2 | **Edge / CDN** | Static assets, tile delivery |
| 3 | **API Layer** | ASP.NET Core API behind ALB |
| 4 | **Auth** | User identity, MFA, session management |
| 5 | **Storage** | Large binaries (WSI files) |
| 6 | **Metadata** | Relational data (jobs, users, audit) |
| 7 | **Job Queue** | Async job dispatch, retries |
| 8 | **Compute** | API hosts, GPU inference workers |
| 9 | **Real-time** | Push job status to frontend |
| 10 | **Monitoring** | Logs, metrics, tracing |

---

## 2. How Components Interact

```
User → Browser → Cognito (login)
       Browser → API (requests)
       Browser → CloudFront (static/tiles)

API → Cognito (validate token)
API → RDS (read/write metadata)
API → S3 (presigned URLs, read results)
API → SQS (publish job)

SQS → EKS workers (consume job)
Workers → S3 (read WSI, write results)
Workers → RDS (update job status)
Workers → WebSocket/AppSync (notify frontend)

WebSocket/AppSync → Browser (real-time updates)
```

**End-to-end flow:**

1. User logs in via **Cognito**
2. Upload WSI via **presigned S3 URL**
3. Metadata stored in **RDS PostgreSQL**
4. API publishes job to **SQS**
5. GPU worker (**EKS**) processes image tiles
6. Results stored in **S3**
7. Job status updated in **RDS**
8. **WebSocket/AppSync** notifies frontend

See [high_level_platform.md](high_level_platform.md) for the Mermaid diagram and [production-architecture.md](production-architecture.md) for deployment topology.

---

## 3. Tools, Services, and Frameworks per Component

| Component | Tool / Service | Notes |
|-----------|---------------|-------|
| Client | **React** (TypeScript) | SPA, component model |
| WSI Viewer | **OpenSeadragon** | OSS, pyramid support, WebGL |
| Edge | **CloudFront** | CDN for static assets and tiles |
| API | **ASP.NET Core** (.NET 10) | REST API, JWT validation |
| Load Balancer | **ALB** | HTTPS termination, routing |
| Auth | **AWS Cognito** | User pools, MFA, B2B |
| Storage | **S3** | Object storage for WSI and results |
| Metadata | **RDS PostgreSQL** | Relational DB |
| Job Queue | **SQS** | Managed message queue |
| Compute | **EKS** (or ECS Fargate) | Containers, GPU node groups |
| Real-time | **SignalR** (current) / **API Gateway WebSocket** | Push to frontend. Pulumi: `enableWebSocketApi=true` for WebSocket API. |
| Monitoring | **CloudWatch**, **X-Ray** | Logs, metrics, tracing. Pulumi: `enableXRayTracing=true` for X-Ray. |

---

## 4. Why These Tools Were Chosen

| Component | Choice | Rationale |
|-----------|--------|------------|
| **S3** | Object storage | Large binaries (WSI); lifecycle policies; HTTP range requests for partial reads; presigned URLs for secure uploads |
| **RDS PostgreSQL** | Metadata DB | Relational model for jobs, users, audit; ACID; row-level security for multi-tenant; managed backups |
| **EKS + GPU nodes** | Inference compute | Orchestration, auto-scaling, native GPU workloads; container portability |
| **SQS** | Job queue | Decouples API from workers; built-in retries; backpressure; fully managed |
| **Cognito** | Auth | Healthcare identity, MFA, compliance (HIPAA); B2B support; managed JWKS |
| **AppSync / WebSocket** | Real-time | Push job status to UI without polling; low latency |
| **CloudFront** | CDN | Tile delivery for WSI viewer; lower latency; reduced origin load |
| **CloudWatch / X-Ray** | Monitoring | Centralized logs, metrics, distributed tracing; AWS-native |
| **OpenSeadragon** | WSI viewer | Mature OSS; pyramid support; WebGL acceleration |
| **React** | Frontend | Component model; TypeScript; ecosystem for medical UIs |
| **ASP.NET Core** | Backend | Performance; .NET ecosystem; strong typing; cross-platform |

---

## Implementation Status

Phase 1 MVP components (per [high_level_platform.md](high_level_platform.md) §2.4 Project Management):

| Component | Status | Location |
|-----------|--------|----------|
| Auth (Cognito) | ✅ | `Features/Auth` |
| Upload pipeline (S3 presigned) | ✅ | `Features/Storage`, `Features/Wsi` |
| WSI metadata & jobs | ✅ | `Features/Wsi` |
| Manual analysis trigger | ✅ | `POST /api/v1/wsi/uploads/{id}/analyze` |
| Job queue (SQS/MassTransit) | ✅ | `WsiAnalysisSaga`, `WsiAnalysisRequestedEvent`; Inbox/Outbox with PostgreSQL |
| WSI viewer (OpenSeadragon) | ✅ | `frontend/features/wsi/components/wsi-viewer.tsx` |
| GPU worker | Mock (dev) / .NET Worker (prod) | `Wsi:UseMockWorker=true` in dev: WsiAnalysisRequestedConsumer simulates completion. Prod: deploy `workers/wsi/HiveOrders.WsiWorker` or Python service per [WSI-GPU-WORKER.md](WSI-GPU-WORKER.md). Pulumi: `enableEksGpu=true` for EKS GPU node group. |

---

## References

- [SCALABLE-GPU-INFERENCE.md](SCALABLE-GPU-INFERENCE.md) – Scalability, cost efficiency, large images, user progress
- [LARGE-FILE-HANDLING.md](LARGE-FILE-HANDLING.md) – WSI performance and bottlenecks (cloud and user-side)
- [high_level_platform.md](high_level_platform.md) – Goals, architecture diagram, GPU inference, project phases
- [production-architecture.md](production-architecture.md) – Deployment topology, config, compliance
- [system-architecture-diagrams.md](system-architecture-diagrams.md) – Domain model, ER, sequence diagrams
