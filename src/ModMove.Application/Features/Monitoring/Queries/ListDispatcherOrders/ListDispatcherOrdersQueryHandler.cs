using Dapper;
using ModMove.Application.Common;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Application.Common.Models;

namespace ModMove.Application.Features.Monitoring.Queries.ListDispatcherOrders;

internal sealed class ListDispatcherOrdersQueryHandler(
    ISqlConnectionFactory connectionFactory,
    TimeProvider clock) : IQueryHandler<ListDispatcherOrdersQuery, PagedResult<DispatcherOrderListItemResponse>>
{
    // Highlight Pending orders once 8 of the 10 matching minutes have elapsed (UC-DIS-01 Step 5, GB-24, DECISIONS 3.2).
    private static readonly TimeSpan MatchingWarningThreshold = TimeSpan.FromMinutes(8);

    private const string From = """
        FROM shipping_orders o
        JOIN users cust ON cust.id = o.customer_id
        JOIN vehicle_types vt ON vt.id = o.vehicle_type_id
        LEFT JOIN driver_profiles dp ON dp.id = o.driver_profile_id
        LEFT JOIN users drv_user ON drv_user.id = dp.user_id
        LEFT JOIN vehicles v ON v.driver_profile_id = dp.id
        LEFT JOIN proof_of_deliveries pod ON pod.order_id = o.id
        WHERE (@Status IS NULL OR o.order_status = @Status)
          AND (@FromUtc IS NULL OR o.created_at >= @FromUtc)
          AND (@ToUtc IS NULL OR o.created_at <= @ToUtc)
          AND (@SearchPattern IS NULL
               OR o.order_code LIKE @SearchPattern
               OR cust.phone_number LIKE @SearchPattern
               OR drv_user.full_name LIKE @SearchPattern
               OR v.license_plate LIKE @SearchPattern)
        """;

    private const string PageSql = """
        SELECT o.id AS Id,
               o.order_code AS OrderCode,
               o.order_status AS OrderStatus,
               o.payment_method AS PaymentMethod,
               o.payment_status AS PaymentStatus,
               o.total_fee AS TotalFee,
               o.pickup_address AS PickupAddress,
               o.dropoff_address AS DropoffAddress,
               cust.full_name AS CustomerName,
               cust.phone_number AS CustomerPhone,
               dp.id AS DriverProfileId,
               drv_user.full_name AS DriverName,
               drv_user.phone_number AS DriverPhone,
               v.license_plate AS LicensePlate,
               vt.name AS VehicleTypeName,
               pod.proof_status AS ProofStatus,
               (SELECT i.id
                FROM in_transit_incidents i
                WHERE i.order_id = o.id
                  AND i.resolution_status IN ('Open', 'InProgress')
                ORDER BY i.id DESC
                LIMIT 1) AS OpenIncidentId,
               o.created_at AS CreatedAt,
               o.paid_at AS PaidAt,
               COUNT(*) OVER() AS TotalCount
        """ + "\n" + From + "\n" + """
        ORDER BY o.created_at DESC, o.id DESC
        LIMIT @PageSize OFFSET @Offset
        """;

    private const string CountSql = "SELECT COUNT(*)\n" + From;

    public async Task<PagedResult<DispatcherOrderListItemResponse>> Handle(
        ListDispatcherOrdersQuery request, CancellationToken cancellationToken)
    {
        var parameters = new
        {
            Status = request.Status?.ToString(),
            FromUtc = request.From?.UtcDateTime,
            ToUtc = request.To?.UtcDateTime,
            SearchPattern = SqlHelpers.LikeContains(request.Search),
            request.PageSize,
            Offset = (request.Page - 1) * request.PageSize,
        };

        using var connection = connectionFactory.CreateConnection();
        var rows = (await connection.QueryAsync<Row>(
            new CommandDefinition(PageSql, parameters, cancellationToken: cancellationToken))).AsList();

        var total = rows.Count > 0
            ? rows[0].TotalCount
            : request.Page > 1
                ? await connection.QueryFirstOrDefaultAsync<int>(new CommandDefinition(CountSql, parameters, cancellationToken: cancellationToken))
                : 0;

        var now = clock.GetUtcNow();
        var items = rows.Select(r =>
        {
            var createdAt = SqlHelpers.ToUtc(r.CreatedAt);
            var paidAt = SqlHelpers.ToUtc(r.PaidAt);

            // DECISIONS 3.2: cash orders start matching at creation; PayOS orders start only once paid.
            var matchingStart = string.Equals(r.PaymentMethod, "PayOS", StringComparison.OrdinalIgnoreCase)
                ? paidAt
                : createdAt;

            var matchingTimeoutSoon = string.Equals(r.OrderStatus, "Pending", StringComparison.OrdinalIgnoreCase)
                && matchingStart is not null
                && now - matchingStart.Value >= MatchingWarningThreshold;

            var awaitingPickupApproval = string.Equals(r.OrderStatus, "PickedUp", StringComparison.OrdinalIgnoreCase)
                && string.Equals(r.ProofStatus, "Pending", StringComparison.OrdinalIgnoreCase);

            return new DispatcherOrderListItemResponse(
                r.Id, r.OrderCode, r.OrderStatus, r.PaymentMethod, r.PaymentStatus, r.TotalFee,
                r.PickupAddress, r.DropoffAddress, r.CustomerName, r.CustomerPhone,
                r.DriverProfileId, r.DriverName, r.DriverPhone, r.LicensePlate, r.VehicleTypeName,
                matchingTimeoutSoon, awaitingPickupApproval, r.OpenIncidentId, createdAt, paidAt);
        }).ToList();

        return new PagedResult<DispatcherOrderListItemResponse>(items, request.Page, request.PageSize, total);
    }

    private sealed class Row
    {
        public long Id { get; init; }
        public string OrderCode { get; init; } = "";
        public string OrderStatus { get; init; } = "";
        public string PaymentMethod { get; init; } = "";
        public string PaymentStatus { get; init; } = "";
        public decimal TotalFee { get; init; }
        public string PickupAddress { get; init; } = "";
        public string DropoffAddress { get; init; } = "";
        public string CustomerName { get; init; } = "";
        public string CustomerPhone { get; init; } = "";
        public long? DriverProfileId { get; init; }
        public string? DriverName { get; init; }
        public string? DriverPhone { get; init; }
        public string? LicensePlate { get; init; }
        public string VehicleTypeName { get; init; } = "";
        public string? ProofStatus { get; init; }
        public long? OpenIncidentId { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? PaidAt { get; init; }
        public int TotalCount { get; init; }
    }
}

