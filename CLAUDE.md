# CLAUDE.md

ModMove backend: REST API + SignalR cho ứng dụng chuyển nhà / giao hàng (Capstone SEP490 - G121).
.NET 8, Clean Architecture + CQRS (MediatR 12), EF Core 8 + MySQL cho ghi, Dapper cho đọc, xUnit.

**Đọc [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) trước khi thêm code.** File đó là nguồn sự thật về layer, CQRS và template use case; file này chỉ bổ sung những gì không có ở đó.

## Lệnh

```bash
dotnet build ModMove.slnx
dotnet test ModMove.slnx
dotnet format ModMove.slnx                     # chạy trước khi commit; CI chạy --verify-no-changes
dotnet run --project src/ModMove.WebApi
dotnet ef migrations add <Name> -p src/ModMove.Infrastructure -s src/ModMove.WebApi -o Persistence/Migrations
```

- Cần .NET SDK 9.0.200+ để đọc `ModMove.slnx` (project vẫn target `net8.0`).
- Một việc chỉ coi là xong khi `dotnet build` không warning mới và `dotnet test` pass, kể cả `ModMove.ArchitectureTests`.

## Quy tắc bắt buộc

- **Hướng phụ thuộc** chỉ vào trong: WebApi → Infrastructure → Application → Domain. `tests/ModMove.ArchitectureTests` sẽ fail nếu vi phạm. Không sửa/nới test kiến trúc để code mới pass.
- **Query handler** chỉ dùng `ISqlConnectionFactory` + Dapper, không inject `IRepository<T>`/`IUnitOfWork`. Command handler dùng repository + `IUnitOfWork.SaveChangesAsync`.
- **Một use case = một thư mục** `Application/Features/<Module>/{Commands|Queries}/<UseCase>/` chứa Command/Query + Handler (+ Validator). MediatR và FluentValidation tự quét assembly, không cần sửa DI.
- Handler và Validator để `internal sealed`; Command/Query/Response DTO là `public sealed record`.
- **Business rule nằm trong entity** (Domain), handler chỉ điều phối. Vi phạm rule → ném `DomainException` (ra 422).
- **Exception**: dùng các exception trong `Application/Common/Exceptions` (`NotFoundException`, `ForbiddenAccessException`, `ValidationException` của project, không phải của FluentValidation). Middleware map sang ProblemDetails; không tự `try/catch` trả lỗi trong controller.
- **Controller mỏng**: kế thừa `ApiControllerBase`, chỉ `Sender.Send(...)`. Đặt theo vai trò trong `Controllers/<Role>/`.
- Luôn truyền `CancellationToken` xuống tới EF/Dapper. Không dùng `.Result`/`.Wait()`.
- Dapper: luôn dùng tham số (`@Id`), không nối chuỗi SQL.
- Tiền tệ dùng `decimal`, không dùng `double`/`float`.
- Không commit secret. Connection string, key PayOS/Cloudinary/Firebase/Google Maps để trong `appsettings.Development.json` / `appsettings.Local.json` / user-secrets (đã gitignore), không để trong `appsettings.json`.
- MediatR giữ ở 12.x (13+ cần license thương mại). Không thêm package mới khi chưa hỏi.

## Test

- Unit test: `tests/ModMove.UnitTests` (tham chiếu Domain + Application). Integration test: `tests/ModMove.IntegrationTests` (dùng `WebApplicationFactory<Program>`).
- Với use case mới: viết test từ yêu cầu trước, xác nhận test fail, rồi mới viết code. Không sửa test cho khớp với bug.
- Ưu tiên test domain rule và validator; module Payments / Wallet phải có test cho case lỗi (thiếu tiền, webhook sai chữ ký, gọi trùng).

## Git

- Nhánh `feature/<name>` → PR vào `develop`. Không push thẳng `main`/`develop`.
- Commit: `[TICKET-ID] <type>: <mô tả ngắn>`, type ∈ `feat|fix|refactor|test|docs|chore`. Ví dụ `[FE-01] feat: implement JWT authentication`.
- Không tự commit/push khi chưa được yêu cầu. PR cần ít nhất 1 peer review.

## Review

Trước khi mở PR, chạy subagent `reviewer` (`.claude/agents/reviewer.md`) để review diff so với `develop` trong một context riêng.
