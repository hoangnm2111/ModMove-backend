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
               OR (@View = 'Active' AND o.order_status NOT IN ('Completed', 'Cancelled')))
          AND (@Pattern IS NULL
               OR o.order_code LIKE @Pattern OR o.pickup_address LIKE @Pattern
               OR o.dropoff_address LIKE @Pattern OR o.receiver_name LIKE @Pattern)
        """;

    private const string CountSql = $"SELECT COUNT(1) FROM shipping_orders o WHERE {Filter}";

    private const string ItemsSql = $"""
        SELECT o.id AS Id, o.order_code AS OrderCode, o.order_status AS OrderStatus,
               o.pickup_address AS PickupAddress, o.dropoff_address AS DropoffAddress, vt.name AS VehicleTypeName,
               o.total_fee AS TotalFee, o.payment_method AS PaymentMethod, o.payment_status AS PaymentStatus,
               o.created_at AS CreatedAt
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
            Pattern = string.IsNullOrWhiteSpace(request.Search) ? null : "%" + EscapeLike(request.Search.Trim()) + "%",
            request.PageSize,
            Offset = (request.Page - 1) * request.PageSize,
        };

        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition(CountSql, parameters, cancellationToken: cancellationToken));
        var rows = await connection.QueryAsync<Row>(new CommandDefinition(ItemsSql, parameters, cancellationToken: cancellationToken));

        var items = rows.Select(r => new OrderSummaryResponse(
            r.Id, r.OrderCode, r.OrderStatus, r.PickupAddress, r.DropoffAddress, r.VehicleTypeName,
            r.TotalFee, r.PaymentMethod, r.PaymentStatus, CustomerAccess.Utc(r.CreatedAt))).ToList();
        return new PagedResult<OrderSummaryResponse>(items, request.Page, request.PageSize, total);
    }

    // MySQL LIKE uses backslash as the escape character: a typed % or _ must match itself.
    private static string EscapeLike(string text) =>
        text.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");

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
    }
}
