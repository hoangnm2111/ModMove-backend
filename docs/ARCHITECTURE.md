# Kiến trúc Backend ModMove

## 1. Clean Architecture / Onion

```
WebApi ──► Infrastructure ──► Application ──► Domain
   └───────────────────────────►┘
```

Phụ thuộc **chỉ hướng vào trong**. `tests/ModMove.ArchitectureTests` sẽ fail CI nếu vi phạm:

| Layer | Được phép tham chiếu | Cấm |
|---|---|---|
| **Domain** | không gì cả | Application, Infrastructure, WebApi, MediatR, EF Core, ASP.NET |
| **Application** | Domain, MediatR, FluentValidation, Dapper | Infrastructure, WebApi, EF Core, ASP.NET |
| **Infrastructure** | Application, Domain, EF Core, SDK bên ngoài | WebApi, ASP.NET |
| **WebApi** | tất cả (composition root) | — |

## 2. Cấu trúc thư mục

```
src/
├── ModMove.Domain/                    # Nghiệp vụ thuần, không framework
│   ├── Common/                        # BaseEntity (Id BIGINT), BaseCreatedEntity, BaseAuditableEntity, IAggregateRoot
│   ├── Entities/                      # Aggregate + entity (ShippingOrder, Trip, DriverWallet...)
│   ├── Enums/
│   ├── Constants/                     # BusinessRules (giới hạn GB-xx)
│   ├── Services/                      # Domain service (FareCalculator, OrderSettlement)
│   └── Exceptions/                    # DomainException (vi phạm business rule → 422)
│
├── ModMove.Application/               # Use case + interface (port)
│   ├── Common/
│   │   ├── Messaging/                 # ICommand, IQuery, ICommandHandler, IQueryHandler
│   │   ├── Behaviors/                 # Pipeline: Logging → Validation → Handler
│   │   ├── Interfaces/                # Port mà Infrastructure/WebApi implement (DIP)
│   │   │   └── Persistence/           # IRepository<T>, IUnitOfWork, ISqlConnectionFactory
│   │   ├── Exceptions/                # NotFound, Forbidden, Validation
│   │   └── Models/                    # PagedResult<T>, DTO dùng chung
│   ├── Features/<Module>/             # Auth, Orders, Matching, Payments, Wallets...
│   │   ├── Commands/<UseCase>/        # Command + Handler + Validator
│   │   └── Queries/<UseCase>/         # Query + Handler + Response DTO
│   └── DependencyInjection.cs         # AddApplication()
│
├── ModMove.Infrastructure/            # Adapter implement interface của Application
│   ├── Persistence/
│   │   ├── ApplicationDbContext.cs    # EF Core, đồng thời là IUnitOfWork
│   │   ├── Configurations/            # IEntityTypeConfiguration<T> cho từng entity
│   │   ├── Interceptors/              # Tự gán CreatedAt/By, UpdatedAt/By
│   │   ├── Repositories/              # Repository<T> (+ repo riêng khi cần)
│   │   ├── Migrations/                # Code first: dotnet ef migrations add ...
│   │   ├── Seed/                      # DatabaseInitializer: migrate + reference data (+ demo accounts in Development)
│   │   └── SqlConnectionFactory.cs    # Kết nối đọc cho Dapper
│   ├── Identity/                      # JWT, BCrypt
│   ├── ExternalServices/              # Cloudinary, GoogleMaps, PayOS, Firebase, Email
│   ├── BackgroundJobs/
│   └── DependencyInjection.cs         # AddInfrastructure()
│
└── ModMove.WebApi/                    # Tầng trình bày HTTP/WebSocket
    ├── Controllers/<Role>/            # Admin, Customer, Driver, Dispatcher, Public, Webhooks (PayOS)
    │   └── ApiControllerBase.cs       # Controller mỏng: HTTP → Sender.Send(...)
    ├── Hubs/                          # SignalR (GPS realtime)
    ├── Middlewares/                   # ExceptionHandlingMiddleware → ProblemDetails
    ├── Services/                      # CurrentUserService (đọc JWT claims)
    └── Program.cs                     # Composition root
```

## 3. CQRS

| | Command (ghi) | Query (đọc) |
|---|---|---|
| Interface | `ICommand` / `ICommand<T>` | `IQuery<T>` |
| Truy cập dữ liệu | `IRepository<T>` + `IUnitOfWork` (EF Core, tracking, transaction) | `ISqlConnectionFactory` + Dapper (không tracking, SELECT không lock trên InnoDB) |
| Trả về | Id / kết quả tối thiểu | DTO tối ưu cho màn hình |

Query handler **không được** inject `IRepository`/`IUnitOfWork`; architecture test sẽ chặn.

## 4. SOLID trong thực tế

- **S**: 1 use case = 1 thư mục = 1 Command/Query + 1 Handler (+ 1 Validator).
- **O**: Tính năng mới = thêm thư mục use case mới. MediatR và FluentValidation tự quét assembly, nên không cần sửa DI hay code cũ.
- **D**: Application khai báo interface (`ISqlConnectionFactory`, `ICurrentUserService`, sau này `IPaymentGateway`, `IMapService`...), Infrastructure/WebApi implement và đăng ký trong `DependencyInjection.cs`.

## 5. Tách rời Client

Backend chỉ cung cấp REST (`/api/...`) + SignalR + `/health`. WebApp và Mobile (Flutter/React Native) là các ứng dụng deploy riêng:
- Origin được phép cấu hình trong `Cors:AllowedOrigins` (appsettings), không hardcode.
- Mọi lỗi trả về cùng một format **RFC 7807 ProblemDetails** (`400` validation, `401` sai thông tin đăng nhập, `403`, `404`, `409` xung đột, `422` domain, `500`) kèm trường `code` ổn định để client rẽ nhánh: `VALIDATION`, `UNAUTHORIZED`, `FORBIDDEN`, `NOT_FOUND`, `CONFLICT`, `BUSINESS_RULE`, `SERVER_ERROR`. Muốn mã riêng (vd `MSG67` trong SRS): `throw new DomainException("...", "MSG67")`.

### Quy ước API (cả nhóm theo)

- **Route và quyền theo thư mục**: `Controllers/Customer/OrdersController` → `api/customer/orders`, chỉ role `Customer`. `Admin` → role `Administrator`. `Account` cần đăng nhập, mọi role (hồ sơ, mật khẩu, đăng xuất). `Public` và `Webhooks` không cần đăng nhập. Thư mục khác 7 thư mục này làm app báo lỗi lúc khởi động. Không tự gắn `[Route]`/`[Authorize]` trừ khi có lý do (`RoleConvention` lo).
- **Xác thực**: `Authorization: Bearer <accessToken>`, lấy ở `POST api/public/auth/login` (15 phút); gia hạn bằng `POST api/public/auth/refresh` với refresh token (30 ngày, dùng một lần). Token chứa `NameIdentifier` (id) và `Role`; trong code dùng `ICurrentUserService` và `currentUser.LoadUserAsync(users, ct)` (từ chối tài khoản không còn Active). Đổi mật khẩu, khóa hay xóa tài khoản phải gọi `IRefreshTokenRepository.RevokeAllForUserAsync`.
- **Id** là số nguyên (`long`). **Enum** trả và nhận dạng chuỗi (`"Pending"`), giống giá trị lưu trong DB. **Tiền** là `decimal` VND. **Ngày giờ** ISO 8601, UTC.
- **Phân trang** (query): `?page=1&pageSize=20`; trả `PagedResult<T>` (`items`, `pageNumber`, `pageSize`, `totalCount`).
- **Use case mẫu để copy**: Command: `Features/Auth/Commands/Login`. Query: `Features/Catalog/Queries/GetVehicleTypes`. Controller: `Controllers/Public/AuthController`.
- **Port ra ngoài** (`IFileStorage`, `IEmailSender`, `IPushNotificationService`, `IPaymentGateway`, `IMapService`, `IRealtimeNotifier`) khai báo ở `Application/Common/Interfaces/Ports.cs`; adapter ở `Infrastructure/ExternalServices/<Provider>`. Người sở hữu adapter được sửa chữ ký cho vừa dịch vụ thật.
- **Repository riêng cho aggregate** khi cần `Include` hay truy vấn riêng: interface ở `Application/Common/Interfaces/Persistence`, class ở `Infrastructure/Persistence/Repositories`, đăng ký trong `DependencyInjection.cs` (mẫu: `IUserRepository`).

## 6. Mẫu thêm một use case

`Application/Features/Orders/Commands/CreateOrder/`

```csharp
public sealed record CreateOrderCommand(string PickupAddress, string DropoffAddress) : ICommand<long>;

internal sealed class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        RuleFor(x => x.PickupAddress).NotEmpty();
        RuleFor(x => x.DropoffAddress).NotEmpty();
    }
}

internal sealed class CreateOrderCommandHandler(IRepository<ShippingOrder> orders, IUnitOfWork unitOfWork)
    : ICommandHandler<CreateOrderCommand, long>
{
    public async Task<long> Handle(CreateOrderCommand request, CancellationToken ct)
    {
        var order = ShippingOrder.Create(request.PickupAddress, request.DropoffAddress);
        orders.Add(order);
        await unitOfWork.SaveChangesAsync(ct);
        return order.Id;
    }
}
```

`Application/Features/Orders/Queries/GetOrderById/`

```csharp
public sealed record GetOrderByIdQuery(long Id) : IQuery<OrderResponse>;

public sealed record OrderResponse(long Id, string PickupAddress, string DropoffAddress, string Status);

internal sealed class GetOrderByIdQueryHandler(ISqlConnectionFactory db)
    : IQueryHandler<GetOrderByIdQuery, OrderResponse>
{
    public async Task<OrderResponse> Handle(GetOrderByIdQuery request, CancellationToken ct)
    {
        using var connection = db.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<OrderResponse>(
                   new CommandDefinition(
                       "SELECT Id, PickupAddress, DropoffAddress, Status FROM ShippingOrders WHERE Id = @Id",
                       request, cancellationToken: ct))
               ?? throw new NotFoundException("ShippingOrder", request.Id);
    }
}
```

`WebApi/Controllers/Customer/OrdersController.cs`

```csharp
public sealed class OrdersController : ApiControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateOrderCommand command, CancellationToken ct)
    {
        var id = await Sender.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpGet("{id:guid}")]
    public async Task<OrderResponse> GetById(long id, CancellationToken ct) =>
        await Sender.Send(new GetOrderByIdQuery(id), ct);
}
```

Entity mới thì thêm `Infrastructure/Persistence/Configurations/ShippingOrderConfiguration.cs`, rồi tạo migration:

```bash
dotnet ef migrations add AddShippingOrder -p src/ModMove.Infrastructure -s src/ModMove.WebApi -o Persistence/Migrations
```
