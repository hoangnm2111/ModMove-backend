# 🚚 ModMove - Backend API Service

[![Backend CI Pipeline (.NET 8)](https://github.com/hoangnm2111/ModMove-backend/actions/workflows/backend-ci.yml/badge.svg)](https://github.com/hoangnm2111/ModMove-backend/actions/workflows/backend-ci.yml)

Core REST API and Real-Time WebSocket backend services for the **ModMove On-Demand Moving and Delivery Application** (Capstone Project SEP490 - Team G121).

---

## 🏛️ Architecture Overview
The backend is structured according to **Clean Architecture** and **Domain-Driven Design (DDD)** principles:

- `src/ModMove.Domain`: Core business entities and aggregates with their business rules, enumerations and fixed limits (`BusinessRules`).
- `src/ModMove.Application`: Use cases, CQRS commands/queries (MediatR), DTOs, FluentValidation validators, repository and external service interfaces.
- `src/ModMove.Infrastructure`: MySQL database persistence (Entity Framework Core), Cloudinary media adapter, Google Maps adapter, PayOS gateway client, FCM adapter, and BCrypt password hasher.
- `src/ModMove.WebApi`: ASP.NET Core Web API controllers, JWT authentication middleware, SignalR/WebSocket hubs for live GPS streaming, and global error handling middleware.
- `tests/ModMove.UnitTests`: Unit tests for domain models and business rules.
- `tests/ModMove.IntegrationTests`: Use-case tests through the real MediatR pipeline with in-memory fakes (no database).
- `tests/ModMove.ArchitectureTests`: Enforces layer dependency rules and CQRS read/write separation.

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for the folder layout, CQRS conventions and a use-case template.

---

## 🛠️ Technology Stack
- **Framework:** .NET 8.0 (C#)
- **Database:** MySQL 8.x
- **ORM:** Entity Framework Core 8
- **Real-Time Streaming:** ASP.NET Core SignalR / WebSocket (GPS coordinates 5-10s interval)
- **Authentication:** JWT Bearer tokens + Refresh token rotation + Role-Based Access Control (RBAC)
- **External Integrations:**
  - Google Maps Geocoding + Distance Matrix (or Routes API)
  - PayOS Payment Gateway (VietQR + HMAC-SHA256 webhook)
  - Firebase Cloud Messaging (FCM) push notifications
  - Cloudinary Media API (Cargo photos & Proof of Delivery evidence)

---

## 📌 Bắt buộc đọc trước khi code (theo thứ tự)
1. **README này**: cách chạy dự án (mục bên dưới).
2. [CLAUDE.md](CLAUDE.md): quy tắc bắt buộc (layer, CQRS, test, commit, review).
3. [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md): cấu trúc thư mục, mẫu use case, **quy ước API** (route và quyền theo thư mục, mã lỗi, enum, phân trang).
4. [docs/DECISIONS.md](docs/DECISIONS.md): **quyết định nghiệp vụ nhóm đã chốt**; khác SRS/SDS thì làm theo file này. Chưa có quyết định thì hỏi nhóm trước khi code.
5. [docs/TEAM-PLAN.md](docs/TEAM-PLAN.md): ai làm gì, ranh giới thư mục, checklist từng người theo phase.
6. [docs/api/README.md](docs/api/README.md): endpoint đã có và bảng mã lỗi.

Khi tài liệu và code khác nhau, sửa tài liệu trong cùng PR.

Code mẫu để copy: Command `src/ModMove.Application/Features/Auth/Commands/Login`, Query `src/ModMove.Application/Features/Catalog/Queries/GetVehicleTypes`, Controller `src/ModMove.WebApi/Controllers/Public/AuthController.cs`.

---

## ▶️ Running the project locally
1. Install the .NET SDK 9.0.200+ (the projects target `net8.0`) and MySQL 8.
2. Copy `src/ModMove.WebApi/appsettings.Development.example.json` to `appsettings.Development.json` (git-ignored) and fill in your MySQL password and a random `Jwt:SigningKey` of at least 32 characters (the sample leaves it empty on purpose: the app refuses to start until you set your own). Optional: fill `Email:*` with Gmail SMTP (use an App Password); if `Email:Host` is empty, Development writes emails, including OTP codes, to the console log instead of sending them. Never commit secrets.
3. `dotnet run --project src/ModMove.WebApi`. In Development the app applies the EF Core migrations, seeds reference data (vehicle types, settings, coupons, master data) and creates four demo accounts: `admin@`, `dispatcher@`, `customer@`, `driver@modmove.local` (password `ModMove@123`).
4. Open Swagger at `http://localhost:5133/swagger`, call `POST /api/public/auth/login` and use the returned token as `Authorization: Bearer <token>`.
5. Before pushing: `dotnet format ModMove.slnx`, `dotnet build ModMove.slnx` (no warnings), `dotnet test ModMove.slnx`.

New migration (always `git pull develop` first, two people creating one at once conflict):
`dotnet ef migrations add <Name> -p src/ModMove.Infrastructure -s src/ModMove.WebApi -o Persistence/Migrations`

---

## 🌿 GitFlow & Contribution Rules
- `main`: Production-ready branch. Direct push is prohibited. Merges occur only via PR from `develop`.
- `develop`: Primary integration branch.
- `feature/<name>`: Individual feature branch (e.g., `feature/hoang`), merged into `develop` by PR.
- **Commit Convention:** `[TICKET-ID] <type>: <short description>`
  - Types: `feat`, `fix`, `refactor`, `test`, `docs`, `chore`
  - Example: `[FE-01] feat: implement JWT authentication and refresh token handler`
- **Quality Gates:** Every PR must pass all CI checks (Build, Lint, Unit Tests) and require at least 1 peer review approval.
