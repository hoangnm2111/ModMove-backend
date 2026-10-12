# đŸ ModMove - Backend API Service

[![Backend CI Pipeline (.NET 8)](https://github.com/hoangnm2111/ModMove-backend/actions/workflows/backend-ci.yml/badge.svg)](https://github.com/hoangnm2111/ModMove-backend/actions/workflows/backend-ci.yml)

Core REST API and Real-Time WebSocket backend services for the **ModMove On-Demand Moving and Delivery Application** (Capstone Project SEP490 - Team G121).

---

## đŸ›ï¸ Architecture Overview
The backend is structured according to **Clean Architecture** and **Domain-Driven Design (DDD)** principles:

- `src/ModMove.Domain`: Core business entities, aggregates, enumerations, domain events, and repository interfaces.
- `src/ModMove.Application`: Use cases, CQRS commands/queries (MediatR), DTOs, FluentValidation validators, and external service interfaces.
- `src/ModMove.Infrastructure`: MySQL database persistence (Entity Framework Core), Cloudinary media adapter, Google Maps adapter, PayOS gateway client, FCM adapter, and BCrypt password hasher.
- `src/ModMove.API`: ASP.NET Core Web API controllers, JWT authentication middleware, SignalR/WebSocket hubs for live GPS streaming, and global error handling middleware.
- `tests/ModMove.UnitTests`: Unit tests for domain models, application handlers, and business logic.

---

## đŸ› ï¸ Technology Stack
- **Framework:** .NET 8.0 (C#)
- **Database:** MySQL 8.x
- **ORM:** Entity Framework Core 8
- **Real-Time Streaming:** ASP.NET Core SignalR / WebSocket (GPS coordinates 5â€“10s interval)
- **Authentication:** JWT Bearer tokens + Refresh token rotation + Role-Based Access Control (RBAC)
- **External Integrations:**
  - Google Maps Geocoding, Distance Matrix & Directions API
  - PayOS Payment Gateway (VietQR + HMAC-SHA256 webhook)
  - Firebase Cloud Messaging (FCM) push notifications
  - Cloudinary Media API (Cargo photos & Proof of Delivery evidence)
  - AI Image Analysis (Cargo vehicle recommendation)

---

## đŸŒ¿ GitFlow & Contribution Rules
- `main`: Production-ready branch. Direct push is prohibited. Merges occur only via PR from `develop` or release branches.
- `develop`: Primary integration branch.
- `feature/<name>`: Individual feature branch (e.g., `feature/hoang`).
- `release/<version>`: Stabilization branch before release into `main`.
- **Commit Convention:** `[TICKET-ID] <type>: <short description>`
  - Types: `feat`, `fix`, `refactor`, `test`, `docs`, `chore`
  - Example: `[FE-01] feat: implement JWT authentication and refresh token handler`
- **Quality Gates:** Every PR must pass all CI checks (Build, Lint, Unit Tests) and require at least 1 peer review approval.