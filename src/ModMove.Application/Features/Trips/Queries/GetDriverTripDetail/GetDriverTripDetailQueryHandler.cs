using System.Data;
using Dapper;
using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Domain.Constants;
using ModMove.Domain.Enums;

namespace ModMove.Application.Features.Trips.Queries.GetDriverTripDetail;

internal sealed class GetDriverTripDetailQueryHandler(
    ISqlConnectionFactory connectionFactory,
    ICurrentUserService currentUser,
    IPlatformSettings settings) : IQueryHandler<GetDriverTripDetailQuery, DriverTripDetailResponse>
{
    public async Task<DriverTripDetailResponse> Handle(
        GetDriverTripDetailQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        using var connection = connectionFactory.CreateConnection();
        var context = await DriverAccess.LoadReadContextAsync(connection, userId, cancellationToken);
        if (context.DriverProfileId is not { } driverProfileId)
            throw new NotFoundException("Order", request.OrderId);

        var commissionRate = await settings.GetDecimalAsync(SettingKeys.CommissionRate, cancellationToken);
        return await DriverTripDetailReader.LoadByOrderIdAsync(
                   connection, request.OrderId, driverProfileId, userId, commissionRate, cancellationToken)
               ?? throw new NotFoundException("Order", request.OrderId);
    }
}

internal sealed class GetActiveDriverTripQueryHandler(
    ISqlConnectionFactory connectionFactory,
    ICurrentUserService currentUser,
    IPlatformSettings settings) : IQueryHandler<GetActiveDriverTripQuery, DriverTripDetailResponse?>
{
    private const string ActiveOrderIdSql = """
        SELECT o.id
        FROM shipping_orders o
        WHERE o.driver_profile_id = @DriverProfileId
          AND o.order_status IN ('Accepted', 'Arrived', 'PickedUp', 'InTransit')
        ORDER BY o.id DESC
        LIMIT 1
        """;

    public async Task<DriverTripDetailResponse?> Handle(
        GetActiveDriverTripQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        using var connection = connectionFactory.CreateConnection();
        var context = await DriverAccess.LoadReadContextAsync(connection, userId, cancellationToken);
        if (context.DriverProfileId is not { } driverProfileId)
            return null;

        var activeOrderId = await connection.QuerySingleOrDefaultAsync<long?>(
            new CommandDefinition(ActiveOrderIdSql, new { DriverProfileId = driverProfileId }, cancellationToken: cancellationToken));
        if (activeOrderId is not { } orderId)
            return null;

        var commissionRate = await settings.GetDecimalAsync(SettingKeys.CommissionRate, cancellationToken);
        return await DriverTripDetailReader.LoadByOrderIdAsync(
            connection, orderId, driverProfileId, userId, commissionRate, cancellationToken);
    }
}

internal static class DriverTripDetailReader
{
    private const string OrderSql = """
        SELECT o.id AS Id,
               o.order_code AS OrderCode,
               o.order_status AS OrderStatus,
               o.pickup_address AS PickupAddress,
               o.pickup_latitude AS PickupLatitude,
               o.pickup_longitude AS PickupLongitude,
               o.dropoff_address AS DropoffAddress,
               o.dropoff_latitude AS DropoffLatitude,
               o.dropoff_longitude AS DropoffLongitude,
               vt.name AS VehicleTypeName,
               o.estimated_distance_km AS EstimatedDistanceKm,
               o.estimated_duration_min AS EstimatedDurationMin,
               o.has_loading_service AS HasLoadingService,
               cu.full_name AS CustomerName,
               cu.phone_number AS CustomerPhone,
               o.receiver_name AS ReceiverName,
               o.receiver_phone AS ReceiverPhone,
               o.distance_fee AS DistanceFee,
               o.service_fee AS ServiceFee,
               o.discount_amount AS DiscountAmount,
               o.total_fee AS TotalFee,
               o.commission_rate AS CommissionRate,
               o.commission_amount AS CommissionAmount,
               o.payment_method AS PaymentMethod,
               o.payer_role AS PayerRole,
               o.payment_status AS PaymentStatus,
               o.created_at AS CreatedAt,
               o.accepted_at AS AcceptedAt,
               o.paid_at AS PaidAt,
               o.delivered_at AS DeliveredAt,
               o.completed_at AS CompletedAt,
               o.cancellation_reason AS CancellationReason,
               r.rating_stars AS RatingStars,
               r.review_comment AS ReviewComment,
               r.created_at AS RatedAt
        FROM shipping_orders o
        JOIN vehicle_types vt ON vt.id = o.vehicle_type_id
        JOIN users cu ON cu.id = o.customer_id
        LEFT JOIN ratings r ON r.order_id = o.id
        WHERE o.id = @OrderId AND o.driver_profile_id = @DriverProfileId
        """;

    private const string CargoSql = """
        SELECT cd.cargo_type AS CargoType,
               m.name AS CargoTypeName,
               cd.quantity AS Quantity,
               cd.weight_kg AS WeightKg,
               cd.length_cm AS LengthCm,
               cd.width_cm AS WidthCm,
               cd.height_cm AS HeightCm,
               cd.photo_urls AS PhotoUrls,
               cd.special_notes AS SpecialNotes
        FROM cargo_details cd
        LEFT JOIN master_data_items m ON m.category = 'CARGO_TYPE' AND m.code = cd.cargo_type
        WHERE cd.order_id = @OrderId
        """;

    private const string ProofSql = """
        SELECT pickup_photo_urls AS PickupPhotoUrls,
               proof_status AS ProofStatus,
               pickup_submission_count AS PickupSubmissionCount,
               rejection_reason AS RejectionReason,
               delivery_photo_url AS DeliveryPhotoUrl,
               delivery_completion_time AS DeliveryCompletionTime
        FROM proof_of_deliveries
        WHERE order_id = @OrderId
        """;

    private const string HandoverSql = """
        SELECT i.latitude AS Latitude, i.longitude AS Longitude
        FROM in_transit_incidents i
        JOIN proof_of_deliveries p ON p.order_id = i.order_id
        WHERE i.order_id = @OrderId AND i.resolution_decision = 'ReassignDriver'
        ORDER BY i.resolved_at DESC, i.id DESC
        LIMIT 1
        """;

    private const string HistorySql = """
        SELECT from_status AS FromStatus,
               to_status AS ToStatus,
               changed_by_role AS ChangedByRole,
               note AS Note,
               created_at AS ChangedAt
        FROM order_status_histories
        WHERE order_id = @OrderId
        ORDER BY created_at, id
        """;

    private const string IncidentsSql = """
        SELECT id AS Id,
               incident_type AS IncidentType,
               description AS Description,
               photo_urls AS PhotoUrls,
               reported_by AS ReportedBy,
               resolution_status AS ResolutionStatus,
               resolution_decision AS ResolutionDecision,
               resolution_note AS ResolutionNote,
               created_at AS CreatedAt,
               resolved_at AS ResolvedAt
        FROM in_transit_incidents
        WHERE order_id = @OrderId
        ORDER BY created_at DESC, id DESC
        """;

    public static async Task<DriverTripDetailResponse?> LoadByOrderIdAsync(
        IDbConnection connection,
        long orderId,
        long driverProfileId,
        long driverUserId,
        decimal currentCommissionRate,
        CancellationToken cancellationToken)
    {
        var parameters = new { OrderId = orderId, DriverProfileId = driverProfileId };
        var o = await connection.QuerySingleOrDefaultAsync<OrderRow>(
            new CommandDefinition(OrderSql, parameters, cancellationToken: cancellationToken));
        if (o is null)
            return null;

        var c = await connection.QuerySingleAsync<CargoRow>(
            new CommandDefinition(CargoSql, parameters, cancellationToken: cancellationToken));
        var p = await connection.QuerySingleOrDefaultAsync<ProofRow>(
            new CommandDefinition(ProofSql, parameters, cancellationToken: cancellationToken));
        var history = await connection.QueryAsync<HistoryRow>(
            new CommandDefinition(HistorySql, parameters, cancellationToken: cancellationToken));
        var incidents = await connection.QueryAsync<IncidentRow>(
            new CommandDefinition(IncidentsSql, parameters, cancellationToken: cancellationToken));

        // DECISIONS 7.2: when reassigned after pickup, the replacement driver navigates to the handover vehicle location.
        GeoPoint? handoverPoint = null;
        if (p is not null && o.OrderStatus is nameof(OrderStatus.Accepted) or nameof(OrderStatus.Arrived))
        {
            var handover = await connection.QueryFirstOrDefaultAsync<HandoverRow>(
                new CommandDefinition(HandoverSql, parameters, cancellationToken: cancellationToken));
            if (handover is { Latitude: { } hLat, Longitude: { } hLon })
                handoverPoint = new GeoPoint(hLat, hLon);
        }

        var rate = o.CommissionRate ?? currentCommissionRate;
        var commission = o.CommissionAmount ?? Math.Round(rate * o.TotalFee, 0, MidpointRounding.AwayFromZero);
        var driverEarning = o.TotalFee - commission;
        var cashToCollect = o.PaymentMethod == nameof(PaymentMethod.Cash)
            && o.PaymentStatus != nameof(PaymentStatus.Paid)
            && o.OrderStatus != nameof(OrderStatus.Cancelled)
            ? o.TotalFee
            : 0m;

        return new DriverTripDetailResponse(
            o.Id,
            o.OrderCode,
            o.OrderStatus,
            o.PickupAddress,
            o.PickupLatitude,
            o.PickupLongitude,
            o.DropoffAddress,
            o.DropoffLatitude,
            o.DropoffLongitude,
            handoverPoint,
            o.VehicleTypeName,
            o.EstimatedDistanceKm,
            o.EstimatedDurationMin,
            o.HasLoadingService,
            o.CustomerName,
            o.CustomerPhone,
            o.ReceiverName,
            o.ReceiverPhone,
            o.DistanceFee,
            o.ServiceFee,
            o.DiscountAmount,
            o.TotalFee,
            rate,
            commission,
            driverEarning,
            o.PaymentMethod,
            o.PayerRole,
            o.PaymentStatus,
            cashToCollect,
            SqlHelpers.ToUtc(o.CreatedAt),
            SqlHelpers.ToUtc(o.AcceptedAt),
            SqlHelpers.ToUtc(o.PaidAt),
            SqlHelpers.ToUtc(o.DeliveredAt),
            SqlHelpers.ToUtc(o.CompletedAt),
            o.CancellationReason,
            new DriverTripCargoResponse(
                c.CargoType,
                c.CargoTypeName,
                c.Quantity,
                c.WeightKg,
                c.LengthCm,
                c.WidthCm,
                c.HeightCm,
                SqlHelpers.ParseJsonList(c.PhotoUrls),
                c.SpecialNotes),
            p is null
                ? null
                : new DriverTripProofResponse(
                    SqlHelpers.ParseJsonList(p.PickupPhotoUrls),
                    p.ProofStatus,
                    p.PickupSubmissionCount,
                    Math.Max(0, BusinessRules.MaxPickupSubmissions - p.PickupSubmissionCount),
                    p.RejectionReason,
                    p.DeliveryPhotoUrl,
                    SqlHelpers.ToUtc(p.DeliveryCompletionTime)),
            history.Select(h => new DriverTripHistoryResponse(
                h.FromStatus, h.ToStatus, h.ChangedByRole, h.Note, SqlHelpers.ToUtc(h.ChangedAt))).ToList(),
            incidents.Select(i => new DriverTripIncidentResponse(
                i.Id,
                i.IncidentType,
                // DECISIONS 8.3: if the customer reported the incident, their words may be about the driver; show generic text.
                i.ReportedBy == driverUserId ? i.Description : "Khách hàng báo cáo sự cố, Điều phối viên đang xử lý.",
                i.ReportedBy == driverUserId ? SqlHelpers.ParseJsonList(i.PhotoUrls) : [],
                i.ReportedBy == driverUserId,
                i.ResolutionStatus,
                i.ResolutionDecision,
                i.ResolutionNote,
                SqlHelpers.ToUtc(i.CreatedAt),
                SqlHelpers.ToUtc(i.ResolvedAt))).ToList(),
            o.RatingStars is { } stars && o.RatedAt is { } ratedAt
                ? new DriverTripRatingResponse(stars, o.ReviewComment, SqlHelpers.ToUtc(ratedAt))
                : null);
    }

    private sealed class OrderRow
    {
        public long Id { get; init; }
        public string OrderCode { get; init; } = "";
        public string OrderStatus { get; init; } = "";
        public string PickupAddress { get; init; } = "";
        public decimal PickupLatitude { get; init; }
        public decimal PickupLongitude { get; init; }
        public string DropoffAddress { get; init; } = "";
        public decimal DropoffLatitude { get; init; }
        public decimal DropoffLongitude { get; init; }
        public string VehicleTypeName { get; init; } = "";
        public decimal EstimatedDistanceKm { get; init; }
        public int EstimatedDurationMin { get; init; }
        public bool HasLoadingService { get; init; }
        public string CustomerName { get; init; } = "";
        public string CustomerPhone { get; init; } = "";
        public string ReceiverName { get; init; } = "";
        public string ReceiverPhone { get; init; } = "";
        public decimal DistanceFee { get; init; }
        public decimal ServiceFee { get; init; }
        public decimal DiscountAmount { get; init; }
        public decimal TotalFee { get; init; }
        public decimal? CommissionRate { get; init; }
        public decimal? CommissionAmount { get; init; }
        public string PaymentMethod { get; init; } = "";
        public string? PayerRole { get; init; }
        public string PaymentStatus { get; init; } = "";
        public DateTime CreatedAt { get; init; }
        public DateTime? AcceptedAt { get; init; }
        public DateTime? PaidAt { get; init; }
        public DateTime? DeliveredAt { get; init; }
        public DateTime? CompletedAt { get; init; }
        public string? CancellationReason { get; init; }
        public int? RatingStars { get; init; }
        public string? ReviewComment { get; init; }
        public DateTime? RatedAt { get; init; }
    }

    private sealed class CargoRow
    {
        public string CargoType { get; init; } = "";
        public string? CargoTypeName { get; init; }
        public int Quantity { get; init; }
        public decimal WeightKg { get; init; }
        public int LengthCm { get; init; }
        public int WidthCm { get; init; }
        public int HeightCm { get; init; }
        public string PhotoUrls { get; init; } = "[]";
        public string? SpecialNotes { get; init; }
    }

    private sealed class ProofRow
    {
        public string? PickupPhotoUrls { get; init; }
        public string ProofStatus { get; init; } = "";
        public int PickupSubmissionCount { get; init; }
        public string? RejectionReason { get; init; }
        public string? DeliveryPhotoUrl { get; init; }
        public DateTime? DeliveryCompletionTime { get; init; }
    }

    private sealed class HandoverRow
    {
        public decimal? Latitude { get; init; }
        public decimal? Longitude { get; init; }
    }

    private sealed class HistoryRow
    {
        public string? FromStatus { get; init; }
        public string ToStatus { get; init; } = "";
        public string ChangedByRole { get; init; } = "";
        public string? Note { get; init; }
        public DateTime ChangedAt { get; init; }
    }

    private sealed class IncidentRow
    {
        public long Id { get; init; }
        public string IncidentType { get; init; } = "";
        public string Description { get; init; } = "";
        public string? PhotoUrls { get; init; }
        public long ReportedBy { get; init; }
        public string ResolutionStatus { get; init; } = "";
        public string? ResolutionDecision { get; init; }
        public string? ResolutionNote { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? ResolvedAt { get; init; }
    }
}

