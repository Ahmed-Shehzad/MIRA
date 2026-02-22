# Scalable GPU-Based Inference

> How ML analysis is implemented in a scalable way when a customer requests analysis. See [high_level_platform.md](high_level_platform.md) for full architecture.

---

## Overview

Inference is triggered when a customer requests analysis. The platform uses a job queue, GPU workers on EKS, S3 for large images, and real-time notifications for progress.

---

## 1. Scalability

| Mechanism | Implementation |
|-----------|----------------|
| **Decoupled API and workers** | API publishes job to SQS; workers consume asynchronously. API stays responsive under load. |
| **Auto-scaling** | EKS GPU node groups scale based on SQS queue length (KEDA or custom metric). More jobs → more workers. |
| **Containerized inference** | Each worker runs in a container; horizontal scaling by adding pods. |
| **Warm pool** | Small baseline of GPU nodes for low latency; burst capacity for peaks. |

**Flow:** Customer request → API creates job → SQS → GPU worker picks up → processes → updates RDS → notifies user.

---

## 2. Cost Efficiency

| Strategy | How |
|----------|-----|
| **Spot / Preemptible instances** | Use spot for non-critical workloads; significant savings vs on-demand. |
| **Scale-to-zero or minimum** | Scale down when queue is empty; avoid idle GPU cost. |
| **Tiered storage** | S3 Standard for active WSIs; Glacier for archives. |
| **Reserved capacity** | For predictable load, use reserved instances. |
| **Tile-based processing** | Only process required tiles; avoid full-image load when possible. |

---

## 3. Handling Large Image Files

WSIs can be 100,000 × 100,000 pixels. Full load into memory is not feasible.

| Approach | Details |
|----------|---------|
| **Tile-based processing** | Split into 256×256 or 512×512 tiles; process tiles independently. |
| **HTTP range requests** | S3 supports range requests; workers fetch only needed regions. |
| **Parallel tile inference** | Multiple GPU workers process tiles in parallel. |
| **Aggregated result stitching** | Combine tile results into final output. |
| **S3 as source** | Workers read directly from S3; no need to copy full image to worker. |

**Upload:** Customer uploads via presigned S3 URL; large file never touches API.

---

## 4. How Users Are Informed About Job Progress or Status

| Channel | Purpose |
|---------|---------|
| **WebSocket / AppSync** | Push notifications when job status changes (queued, running, completed, failed). No polling. |
| **Job status API** | `GET /api/v1/wsi/jobs/{id}` for polling fallback when WebSocket unavailable. |
| **Email alerts** | Optional email on completion for long-running jobs. |
| **RDS metadata** | Job status stored in RDS; workers update as they progress. |

**Flow:** Worker updates job in RDS → backend pushes via WebSocket/AppSync → frontend receives and updates UI.

---

## References

- [high_level_platform.md](high_level_platform.md) – Section 2.2, 2.3; architecture diagram; system flow
- [SYSTEM-ARCHITECTURE.md](SYSTEM-ARCHITECTURE.md) – Components and interactions
- [production-architecture.md](production-architecture.md) – Deployment topology
