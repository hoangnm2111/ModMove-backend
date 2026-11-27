using Dapper;
using ModMove.Application.Common;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Application.Common.Models;
using ModMove.Application.Features.Orders;
using ModMove.Domain.Entities;

namespace ModMove.Application.Features.Trips.Queries.GetDriverTrips;

internal sealed class GetDriverTripsQueryHandler(
    ISqlConnectionFactory connectionFactory,
    ICurrentUserService currentUser,
    IPlatformSettings settings) : IQueryHandler<GetDriverTripsQuery, PagedResult<DriverTripSummaryResponse>>
{
    private const string Filter = """
        o.driver_profile_id = @DriverProfileId
          AND (@View = 'All'
               OR (@View = 'History' AND o.order_status IN ('Completed', 'Cancelled'))
               OR (@View = 'Active' AND o.order_status NOT IN ('Completed', 'Cancelled'))
               OR (@View = 'Completed' AND o.order_status = 'Completed')
               OR (@View = 'Cancelled' AND o.order_status = 'Cancelled'))
          AND (@Pattern IS NULL
               OR o.order_code LIKE @Pattern OR o.pickup_address LIKE @Pattern
               OR o.dropoff_address LIKE @Pattern OR o.receiver_name LIKE @Pattern
               OR cu.full_name LIKE @Pattern)
          AND (@Status IS NULL OR o.order_status = @Status)
          AND (@FromUtc IS NULL OR o.created_at >= @FromUtc)
          AND (@ToUtc IS NULL OR o.created_at < @ToUtc)
        """;

    private const string CountSql = $"""
        SELECT COUNT(1)
        FROM shipping_orders o
        JOIN users cu ON cu.id = o.customer_id
        WHERE {Filter}
        """;

    private const string ItemsSql = $"""
        SELECT o.id AS Id,
               o.order_code AS OrderCode,
               o.order_status AS OrderStatus,
               o.pickup_address AS PickupAddress,
               o.dropoff_address AS DropoffAddress,
               vt.name AS VehicleTypeName,
               o.estimated_distance_km AS EstimatedDistanceKm,
               o.estimated_duration_min AS EstimatedDurationMin,
               o.has_loading_service AS HasLoadingService,
               o.total_fee AS TotalFee,
               o.commission_amount AS CommissionAmount,
               o.payment_method AS PaymentMethod,
               o.payer_role AS PayerRole,
               o.payment_status AS PaymentStatus,
               cu.full_name AS CustomerName,
               cu.phone_number AS CustomerPhone,
               o.receiver_name AS ReceiverName,
               o.receiver_phone AS ReceiverPhone,
               o.created_at AS CreatedAt,
               o.delivered_at AS DeliveredAt,
               o.completed_at AS CompletedAt
        FROM shipping_orders o
        JOIN vehicle_types vt ON vt.id = o.vehicle_type_id
        JOIN users cu ON cu.id = o.customer_id
        WHERE {Filter}
        ORDER BY o.created_at DESC, o.id DESC
        LIMIT @PageSize OFFSET @Offset
        """;

    public async Task<PagedResult<DriverTripSummaryResponse>> Handle(
        GetDriverTripsQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        using var connection = connectionFactory.CreateConnection();
        var context = await DriverAccess.LoadReadContextAsync(connection, userId, cancellationToken);
        if (context.DriverProfileId is not { } driverProfileId)
            return new PagedResult<DriverTripSummaryResponse>([], request.Page, request.PageSize, 0);

        var commissionRate = await settings.GetDecimalAsync(SettingKeys.CommissionRate, cancellationToken);
        var parameters = new
        {
            DriverProfileId = driverProfileId,
            View = request.View.ToString(),
            Pattern = SqlHelpers.LikeContains(request.Search),
            Status = request.Status?.ToString(),
            FromUtc = request.FromDate is { } from ? OrderCode.VietnamDayStartUtc(from).UtcDateTime : (DateTime?)null,
            ToUtc = request.ToDate is { } to ? OrderCode.VietnamDayStartUtc(to.AddDays(1)).UtcDateTime : (DateTime?)null,
            request.PageSize,
            Offset = (request.Page - 1) * request.PageSize,
        };

        var total = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(CountSql, parameters, cancellationToken: cancellationToken));
        if (total == 0)
            return new PagedResult<DriverTripSummaryResponse>([], request.Page, request.PageSize, 0);

        var rows = await connection.QueryAsync<Row>(
            new CommandDefinition(ItemsSql, parameters, cancellationToken: cancellationToken));

        var items = rows.Select(r =>
        {
            var commission = r.CommissionAmount ?? ShippingOrder.CommissionFor(commissionRate, r.TotalFee);
            var driverEarning = r.TotalFee - commission;
            return new DriverTripSummaryResponse(
                r.Id,
                r.OrderCode,
                r.OrderStatus,
                r.PickupAddress,
                r.DropoffAddress,
                r.VehicleTypeName,
                r.EstimatedDistanceKm,
                r.EstimatedDurationMin,
                r.HasLoadingService,
                r.TotalFee,
                commission,
                driverEarning,
                r.PaymentMethod,
                r.PayerRole,
                r.PaymentStatus,
                r.CustomerName,
                r.CustomerPhone,
                r.ReceiverName,
                r.ReceiverPhone,
                SqlHelpers.ToUtc(r.CreatedAt),
                SqlHelpers.ToUtc(r.DeliveredAt),
                SqlHelpers.ToUtc(r.CompletedAt));
        }).ToList();

        return new PagedResult<DriverTripSummaryResponse>(items, request.Page, request.PageSize, total);
    }

    private sealed class Row
    {
        public long Id { get; init; }
        public string OrderCode { get; init; } = "";
        public string OrderStatus { get; init; } = "";
        public string PickupAddress { get; init; } = "";
        public string DropoffAddress { get; init; } = "";
        public string VehicleTypeName { get; init; } = "";
        public decimal EstimatedDistanceKm { get; init; }
        public int EstimatedDurationMin { get; init; }
        public bool HasLoadingService { get; init; }
        public decimal TotalFee { get; init; }
        public decimal? CommissionAmount { get; init; }
        public string PaymentMethod { get; init; } = "";
        public string? PayerRole { get; init; }
        public string PaymentStatus { get; init; } = "";
        public string CustomerName { get; init; } = "";
        public string CustomerPhone { get; init; } = "";
        public string ReceiverName { get; init; } = "";
        public string ReceiverPhone { get; init; } = "";
        public DateTime CreatedAt { get; init; }
        public DateTime? DeliveredAt { get; init; }
        public DateTime? CompletedAt { get; init; }
    }
}

