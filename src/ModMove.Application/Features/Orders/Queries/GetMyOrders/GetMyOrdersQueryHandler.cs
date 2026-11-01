using Dapper;
using ModMove.Application.Common;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Application.Common.Models;

namespace ModMove.Application.Features.Orders.Queries.GetMyOrders;

internal sealed class GetMyOrdersQueryHandler(ISqlConnectionFactory connectionFactory, ICurrentUserService currentUser)
    : IQueryHandler<GetMyOrdersQuery, PagedResult<OrderSummaryResponse>>
{
    // One fixed SQL text: the view and the search are parameters, never pasted into the SQL.
    private const string Filter = """
        o.customer_id = @CustomerId
          AND (@View = 'All'
               OR (@View = 'History' AND o.order_status IN ('Completed', 'Cancelled'))
               OR (@View = 'Active' AND o.order_status NOT IN ('Completed', 'Cancelled'))
               OR (@View = 'Completed' AND o.order_status = 'Completed')
               OR (@View = 'Cancelled' AND o.order_status = 'Cancelled'))
          AND (@Pattern IS NULL
               OR o.order_code LIKE @Pattern OR o.pickup_address LIKE @Pattern
               OR o.dropoff_address LIKE @Pattern OR o.receiver_name LIKE @Pattern)
          AND (@Status IS NULL OR o.order_status = @Status)
          AND (@FromUtc IS NULL OR o.created_at >= @FromUtc)
          AND (@ToUtc IS NULL OR o.created_at < @ToUtc)
          AND (@PhonePattern IS NULL OR o.receiver_phone LIKE @PhonePattern)
        """;

    private const string CountSql = $"SELECT COUNT(1) FROM shipping_orders o WHERE {Filter}";

    private const string ItemsSql = $"""
        SELECT o.id AS Id, o.order_code AS OrderCode, o.order_status AS OrderStatus,
               o.pickup_address AS PickupAddress, o.dropoff_address AS DropoffAddress, vt.name AS VehicleTypeName,
               o.total_fee AS TotalFee, o.payment_method AS PaymentMethod, o.payment_status AS PaymentStatus,
               o.created_at AS CreatedAt, o.receiver_name AS ReceiverName, o.receiver_phone AS ReceiverPhone
        FROM shipping_orders o
        JOIN vehicle_types vt ON vt.id = o.vehicle_type_id
        WHERE {Filter}
        ORDER BY o.created_at DESC, o.id DESC
        LIMIT @PageSize OFFSET @Offset
        """;

    public async Task<PagedResult<OrderSummaryResponse>> Handle(GetMyOrdersQuery request, CancellationToken cancellationToken)
    {
        var customerId = currentUser.RequireUserId();
        using var connection = connectionFactory.CreateConnection();
        await CustomerAccess.EnsureActiveAsync(connection, customerId, cancellationToken);

        var parameters = new
        {
            CustomerId = customerId,
            View = request.View.ToString(),
            Pattern = SqlHelpers.LikeContains(request.Search),
            Status = request.Status?.ToString(),
            // ToDate is included: the range ends at 00:00 Vietnam time of the next day.
            FromUtc = request.FromDate is { } from ? OrderCode.VietnamDayStartUtc(from).UtcDateTime : (DateTime?)null,
            ToUtc = request.ToDate is { } to ? OrderCode.VietnamDayStartUtc(to.AddDays(1)).UtcDateTime : (DateTime?)null,
            PhonePattern = SqlHelpers.LikeContains(request.ReceiverPhone),
            request.PageSize,
            Offset = (request.Page - 1) * request.PageSize,
        };

        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition(CountSql, parameters, cancellationToken: cancellationToken));
        var rows = await connection.QueryAsync<Row>(new CommandDefinition(ItemsSql, parameters, cancellationToken: cancellationToken));

        var items = rows.Select(r => new OrderSummaryResponse(
            r.Id, r.OrderCode, r.OrderStatus, r.PickupAddress, r.DropoffAddress, r.VehicleTypeName,
            r.TotalFee, r.PaymentMethod, r.PaymentStatus, CustomerAccess.Utc(r.CreatedAt), r.ReceiverName, r.ReceiverPhone)).ToList();
        return new PagedResult<OrderSummaryResponse>(items, request.Page, request.PageSize, total);
    }

    private sealed class Row
    {
        public long Id { get; init; }
        public string OrderCode { get; init; } = "";
        public string OrderStatus { get; init; } = "";
        public string PickupAddress { get; init; } = "";
        public string DropoffAddress { get; init; } = "";
        public string VehicleTypeName { get; init; } = "";
        public decimal TotalFee { get; init; }
        public string PaymentMethod { get; init; } = "";
        public string PaymentStatus { get; init; } = "";
        public DateTime CreatedAt { get; init; }
        public string ReceiverName { get; init; } = "";
        public string ReceiverPhone { get; init; } = "";
    }
}
