# MIRA Documentation

Documentation for the **MIRA** project – HIVE Food Ordering Coordination System with WSI (Whole Slide Image) support.

## System Scope

MIRA consists of two integrated capabilities:

1. **HIVE Food Ordering** (primary) – Order rounds, items, payments, recurring orders, Teams bot, notifications
2. **WSI Platform** (extended) – Whole Slide Image upload, analysis jobs, viewer (OpenSeadragon)

Both share the same backend (ASP.NET Core), database (PostgreSQL), auth (Cognito), and infrastructure (AWS).

## Document Index

| Document | Purpose |
|----------|---------|
| [README.md](../README.md) | Project overview, quick start |
| [approach.md](approach.md) | MVP implementation approach, decisions, trade-offs |
| [future_features.md](future_features.md) | Implemented features, roadmap |
| [system-architecture-diagrams.md](system-architecture-diagrams.md) | UML diagrams (domain model, ER, sequence, process) for food ordering |
| [production-config.md](production-config.md) | Environment variables, config structure (X-Ray, WebSocket, IRSA) |
| [production-architecture.md](production-architecture.md) | Deployment topology, CI/CD, EKS worker, auto-scaling, custom domains, tracing |
| [high_level_platform.md](high_level_platform.md) | AWS cloud architecture for WSI/Digital Pathology platform |
| [SYSTEM-ARCHITECTURE.md](SYSTEM-ARCHITECTURE.md) | Component overview, tools, implementation status |
| [SCALABLE-GPU-INFERENCE.md](SCALABLE-GPU-INFERENCE.md) | GPU inference scalability for WSI analysis |
| [WSI-GPU-WORKER.md](WSI-GPU-WORKER.md) | Production GPU worker deployment (.NET, Python, EKS) |
| [LARGE-FILE-HANDLING.md](LARGE-FILE-HANDLING.md) | WSI performance and bottlenecks |

## API Versioning

All API routes use URL path versioning: `api/v{version}/...`. Use `api/v1` for the current version.

## Quick Links

- [DOCUMENTATION.md](../DOCUMENTATION.md) – Combined documentation (setup, run, architecture)
- [infrastructure/README.md](../infrastructure/README.md) – Pulumi IaC deployment
