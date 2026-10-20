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
│   ├── Common/                        # BaseEntity, BaseAuditableEntity, IAggregateRoot
│   ├── Entities/                      # Aggregate + entity (ShippingOrder, Trip, DriverWallet...)
│   ├── Enums/
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
│   │   ├── Migrations/
│   │   └── SqlConnectionFactory.cs    # Kết nối đọc cho Dapper
│   ├── Identity/                      # JWT, BCrypt
│   ├── ExternalServices/              # Cloudinary, GoogleMaps, PayOS, Firebase, Email
│   ├── BackgroundJobs/
│   └── DependencyInjection.cs         # AddInfrastructure()
│
└── ModMove.WebApi/                    # Tầng trình bày HTTP/WebSocket
    ├── Controllers/<Role>/            # Admin, Customer, Driver, Dispatcher, Public
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
- Mọi lỗi trả về cùng một format **RFC 7807 ProblemDetails** (`400` validation, `404`, `403`, `422` domain, `500`).

## 6. Mẫu thêm một use case

`Application/Features/Orders/Commands/CreateOrder/`

```csharp
public sealed record CreateOrderCommand(string PickupAddress, string DropoffAddress) : ICommand<Guid>;

internal sealed class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        RuleFor(x => x.PickupAddress).NotEmpty();
        RuleFor(x => x.DropoffAddress).NotEmpty();
    }
}

internal sealed class CreateOrderCommandHandler(IRepository<ShippingOrder> orders, IUnitOfWork unitOfWork)
    : ICommandHandler<CreateOrderCommand, Guid>
{
    public async Task<Guid> Handle(CreateOrderCommand request, CancellationToken ct)
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
public sealed record GetOrderByIdQuery(Guid Id) : IQuery<OrderResponse>;

public sealed record OrderResponse(Guid Id, string PickupAddress, string DropoffAddress, string Status);

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
    public async Task<OrderResponse> GetById(Guid id, CancellationToken ct) =>
        await Sender.Send(new GetOrderByIdQuery(id), ct);
}
```

Entity mới thì thêm `Infrastructure/Persistence/Configurations/ShippingOrderConfiguration.cs`, rồi tạo migration:

```bash
dotnet ef migrations add AddShippingOrder -p src/ModMove.Infrastructure -s src/ModMove.WebApi -o Persistence/Migrations
```
