using Dapper;
using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Domain.Entities;

namespace ModMove.Application.Features.Monitoring.Queries.GetDispatcherOrderById;

internal sealed class GetDispatcherOrderByIdQueryHandler(ISqlConnectionFactory connectionFactory)
    : IQueryHandler<GetDispatcherOrderByIdQuery, DispatcherOrderDetailResponse>
{
    private const string OrderSql = """
        SELECT o.id AS Id,
               o.order_code AS OrderCode,
               o.order_status AS OrderStatus,
               o.vehicle_type_id AS VehicleTypeId,
               vt.name AS VehicleTypeName,
               o.pickup_address AS PickupAddress,
               o.pickup_latitude AS PickupLatitude,
               o.pickup_longitude AS PickupLongitude,
               o.dropoff_address AS DropoffAddress,
               o.dropoff_latitude AS DropoffLatitude,
               o.dropoff_longitude AS DropoffLongitude,
               o.receiver_name AS ReceiverName,
               o.receiver_phone AS ReceiverPhone,
               o.sender_name AS SenderName,
               o.sender_phone AS SenderPhone,
               o.pickup_note AS PickupNote,
               o.dropoff_note AS DropoffNote,
               o.estimated_distance_km AS EstimatedDistanceKm,
               o.estimated_duration_min AS EstimatedDurationMin,
               o.has_loading_service AS HasLoadingService,
               o.distance_fee AS DistanceFee,
               o.service_fee AS ServiceFee,
               o.discount_amount AS DiscountAmount,
               o.total_fee AS TotalFee,
               o.payment_method AS PaymentMethod,
               o.payer_role AS PayerRole,
               o.payment_status AS PaymentStatus,
               o.cancellation_reason AS CancellationReason,
               o.commission_rate AS CommissionRate,
               o.commission_amount AS CommissionAmount,
               o.created_at AS CreatedAt,
               o.paid_at AS PaidAt,
               o.accepted_at AS AcceptedAt,
               o.delivered_at AS DeliveredAt,
               o.completed_at AS CompletedAt,
               cust.id AS CustomerId,
               cust.full_name AS CustomerFullName,
               cust.phone_number AS CustomerPhone,
               cust.email AS CustomerEmail,
               dp.id AS DriverProfileId,
               drv_user.id AS DriverUserId,
               drv_user.full_name AS DriverFullName,
               drv_user.phone_number AS DriverPhone,
               v.license_plate AS LicensePlate,
               dp.last_latitude AS DriverLastLatitude,
               dp.last_longitude AS DriverLastLongitude,
               dp.last_location_at AS DriverLastLocationAt,
               c.cargo_type AS CargoType,
               m_cargo.name AS CargoTypeName,
               c.quantity AS CargoQuantity,
               c.weight_kg AS CargoWeightKg,
               c.length_cm AS CargoLengthCm,
               c.width_cm AS CargoWidthCm,
               c.height_cm AS CargoHeightCm,
               c.photo_urls AS CargoPhotoUrlsJson,
               c.special_notes AS CargoSpecialNotes,
               pod.pickup_photo_urls AS PickupPhotoUrlsJson,
               pod.proof_status AS ProofStatus,
               pod.pickup_submission_count AS PickupSubmissionCount,
               pod.rejection_reason AS RejectionReason,
               pod.delivery_photo_url AS DeliveryPhotoUrl,
               pod.delivery_completion_time AS DeliveryCompletionTime
        FROM shipping_orders o
        JOIN vehicle_types vt ON vt.id = o.vehicle_type_id
        JOIN users cust ON cust.id = o.customer_id
        LEFT JOIN cargo_details c ON c.order_id = o.id
        LEFT JOIN master_data_items m_cargo ON m_cargo.category = 'CARGO_TYPE' AND m_cargo.code = c.cargo_type
        LEFT JOIN driver_profiles dp ON dp.id = o.driver_profile_id
        LEFT JOIN users drv_user ON drv_user.id = dp.user_id
        LEFT JOIN vehicles v ON v.driver_profile_id = dp.id
        LEFT JOIN proof_of_deliveries pod ON pod.order_id = o.id
        WHERE o.id = @Id;
        """;

    private const string HistorySql = """
        SELECT h.id AS Id,
               h.from_status AS FromStatus,
               h.to_status AS ToStatus,
               h.changed_by_role AS ChangedByRole,
               h.changed_by_user_id AS ChangedByUserId,
               u.full_name AS ChangedByUserName,
               h.note AS Note,
               h.created_at AS CreatedAt
        FROM order_status_histories h
        LEFT JOIN users u ON u.id = h.changed_by_user_id
        WHERE h.order_id = @Id
        ORDER BY h.id ASC;
        """;

    private const string PaymentsSql = """
        SELECT p.id AS Id,
               p.payment_method AS PaymentMethod,
               p.payer_role AS PayerRole,
               p.amount AS Amount,
               p.payos_order_code AS PayosOrderCode,
               p.transaction_status AS TransactionStatus,
               p.paid_at AS PaidAt,
               p.created_at AS CreatedAt
        FROM payment_transactions p
        WHERE p.order_id = @Id
        ORDER BY p.id DESC;
        """;

    private const string IncidentsSql = """
        SELECT i.id AS Id,
               i.incident_type AS IncidentType,
               i.description AS Description,
               i.resolution_status AS ResolutionStatus,
               i.resolution_decision AS ResolutionDecision,
               i.resolution_note AS ResolutionNote,
               reporter.full_name AS ReporterName,
               handler.full_name AS HandledByName,
               i.created_at AS CreatedAt,
               i.resolved_at AS ResolvedAt
        FROM in_transit_incidents i
        JOIN users reporter ON reporter.id = i.reported_by
        LEFT JOIN users handler ON handler.id = i.handled_by
        WHERE i.order_id = @Id
        ORDER BY i.id DESC;
        """;

    private const string RefundsSql = """
        SELECT r.id AS Id,
               r.incident_id AS IncidentId,
               r.source_type AS SourceType,
               r.reason AS Reason,
               r.requested_amount AS RequestedAmount,
               r.proposed_amount AS ProposedAmount,
               r.refund_status AS RefundStatus,
               r.created_at AS CreatedAt,
               r.decided_at AS DecidedAt,
               r.processed_at AS ProcessedAt
        FROM refunds r
        WHERE r.order_id = @Id
        ORDER BY r.id DESC;
        """;

    public async Task<DispatcherOrderDetailResponse> Handle(
        GetDispatcherOrderByIdQuery request, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var parameters = new { request.Id };

        var row = await connection.QuerySingleOrDefaultAsync<OrderRow>(
            new CommandDefinition(OrderSql, parameters, cancellationToken: cancellationToken))
            ?? throw new NotFoundException(nameof(ShippingOrder), request.Id);

        var historyRows = await connection.QueryAsync<HistoryRow>(
            new CommandDefinition(HistorySql, parameters, cancellationToken: cancellationToken));

        var paymentRows = await connection.QueryAsync<PaymentRow>(
            new CommandDefinition(PaymentsSql, parameters, cancellationToken: cancellationToken));

        var incidentRows = await connection.QueryAsync<IncidentRow>(
            new CommandDefinition(IncidentsSql, parameters, cancellationToken: cancellationToken));

        var refundRows = await connection.QueryAsync<RefundItemRow>(
            new CommandDefinition(RefundsSql, parameters, cancellationToken: cancellationToken));

        var customer = new DispatcherOrderCustomerDetails(
            row.CustomerId, row.CustomerFullName, row.CustomerPhone, row.CustomerEmail);

        var receiver = new DispatcherOrderReceiverDetails(row.ReceiverName, row.ReceiverPhone);

        var driver = row.DriverProfileId is { } profileId && row.DriverUserId is { } driverUserId
            ? new DispatcherOrderDriverDetails(
                profileId, driverUserId, row.DriverFullName ?? string.Empty, row.DriverPhone ?? string.Empty,
                row.LicensePlate, row.DriverLastLatitude, row.DriverLastLongitude, SqlHelpers.ToUtc(row.DriverLastLocationAt))
            : null;

        var cargo = row.CargoType is not null
            ? new DispatcherOrderCargoDetails(
                row.CargoType, row.CargoQuantity ?? 0, row.CargoWeightKg ?? 0m,
                row.CargoLengthCm ?? 0, row.CargoWidthCm ?? 0, row.CargoHeightCm ?? 0,
                SqlHelpers.ParseJsonList(row.CargoPhotoUrlsJson), row.CargoSpecialNotes,
                row.CargoTypeName)
            : null;

        var proof = row.ProofStatus is not null
            ? new DispatcherOrderProofDetails(
                SqlHelpers.ParseJsonList(row.PickupPhotoUrlsJson),
                row.ProofStatus,
                row.PickupSubmissionCount ?? 0,
                row.RejectionReason,
                row.DeliveryPhotoUrl,
                SqlHelpers.ToUtc(row.DeliveryCompletionTime))
            : null;

        return new DispatcherOrderDetailResponse(
            row.Id, row.OrderCode, row.OrderStatus, row.VehicleTypeId, row.VehicleTypeName,
            row.PickupAddress, row.PickupLatitude, row.PickupLongitude,
            row.DropoffAddress, row.DropoffLatitude, row.DropoffLongitude,
            row.EstimatedDistanceKm, row.EstimatedDurationMin, row.HasLoadingService,
            row.DistanceFee, row.ServiceFee, row.DiscountAmount, row.TotalFee,
            row.PaymentMethod, row.PayerRole, row.PaymentStatus, row.CancellationReason,
            row.CommissionRate, row.CommissionAmount,
            SqlHelpers.ToUtc(row.CreatedAt), SqlHelpers.ToUtc(row.PaidAt), SqlHelpers.ToUtc(row.AcceptedAt),
            SqlHelpers.ToUtc(row.DeliveredAt), SqlHelpers.ToUtc(row.CompletedAt),
            customer, receiver, driver, cargo, proof,
            historyRows.Select(h => new DispatcherOrderStatusHistoryItem(
                h.Id, h.FromStatus, h.ToStatus, h.ChangedByRole, h.ChangedByUserId, h.ChangedByUserName,
                h.Note, SqlHelpers.ToUtc(h.CreatedAt))).ToList(),
            paymentRows.Select(p => new DispatcherOrderPaymentItem(
                p.Id, p.PaymentMethod, p.PayerRole, p.Amount, p.PayosOrderCode, p.TransactionStatus,
                SqlHelpers.ToUtc(p.PaidAt), SqlHelpers.ToUtc(p.CreatedAt))).ToList(),
            incidentRows.Select(i => new DispatcherOrderIncidentItem(
                i.Id, i.IncidentType, i.Description, i.ResolutionStatus, i.ResolutionDecision, i.ResolutionNote,
                i.ReporterName, i.HandledByName, SqlHelpers.ToUtc(i.CreatedAt), SqlHelpers.ToUtc(i.ResolvedAt))).ToList(),
            refundRows.Select(r => new DispatcherOrderRefundItem(
                r.Id, r.IncidentId, r.SourceType, r.Reason, r.RequestedAmount, r.ProposedAmount, r.RefundStatus,
                SqlHelpers.ToUtc(r.CreatedAt), SqlHelpers.ToUtc(r.DecidedAt), SqlHelpers.ToUtc(r.ProcessedAt))).ToList(),
            row.SenderName is null ? null : new DispatcherOrderSenderDetails(row.SenderName, row.SenderPhone ?? string.Empty),
            row.PickupNote,
            row.DropoffNote);
    }

    private sealed class OrderRow
    {
        public long Id { get; init; }
        public string OrderCode { get; init; } = "";
        public string OrderStatus { get; init; } = "";
        public long VehicleTypeId { get; init; }
        public string VehicleTypeName { get; init; } = "";
        public string PickupAddress { get; init; } = "";
        public decimal PickupLatitude { get; init; }
        public decimal PickupLongitude { get; init; }
        public string DropoffAddress { get; init; } = "";
        public decimal DropoffLatitude { get; init; }
        public decimal DropoffLongitude { get; init; }
        public string ReceiverName { get; init; } = "";
        public string ReceiverPhone { get; init; } = "";
        public string? SenderName { get; init; }
        public string? SenderPhone { get; init; }
        public string? PickupNote { get; init; }
        public string? DropoffNote { get; init; }
        public decimal EstimatedDistanceKm { get; init; }
        public int EstimatedDurationMin { get; init; }
        public bool HasLoadingService { get; init; }
        public decimal DistanceFee { get; init; }
        public decimal ServiceFee { get; init; }
        public decimal DiscountAmount { get; init; }
        public decimal TotalFee { get; init; }
        public string PaymentMethod { get; init; } = "";
        public string? PayerRole { get; init; }
        public string PaymentStatus { get; init; } = "";
        public string? CancellationReason { get; init; }
        public decimal? CommissionRate { get; init; }
        public decimal? CommissionAmount { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? PaidAt { get; init; }
        public DateTime? AcceptedAt { get; init; }
        public DateTime? DeliveredAt { get; init; }
        public DateTime? CompletedAt { get; init; }
        public long CustomerId { get; init; }
        public string CustomerFullName { get; init; } = "";
        public string CustomerPhone { get; init; } = "";
        public string CustomerEmail { get; init; } = "";
        public long? DriverProfileId { get; init; }
        public long? DriverUserId { get; init; }
        public string? DriverFullName { get; init; }
        public string? DriverPhone { get; init; }
        public string? LicensePlate { get; init; }
        public decimal? DriverLastLatitude { get; init; }
        public decimal? DriverLastLongitude { get; init; }
        public DateTime? DriverLastLocationAt { get; init; }
        public string? CargoType { get; init; }
        public string? CargoTypeName { get; init; }
        public int? CargoQuantity { get; init; }
        public decimal? CargoWeightKg { get; init; }
        public int? CargoLengthCm { get; init; }
        public int? CargoWidthCm { get; init; }
        public int? CargoHeightCm { get; init; }
        public string? CargoPhotoUrlsJson { get; init; }
        public string? CargoSpecialNotes { get; init; }
        public string? PickupPhotoUrlsJson { get; init; }
        public string? ProofStatus { get; init; }
        public int? PickupSubmissionCount { get; init; }
        public string? RejectionReason { get; init; }
        public string? DeliveryPhotoUrl { get; init; }
        public DateTime? DeliveryCompletionTime { get; init; }
    }

    private sealed class HistoryRow
    {
        public long Id { get; init; }
        public string? FromStatus { get; init; }
        public string ToStatus { get; init; } = "";
        public string ChangedByRole { get; init; } = "";
        public long? ChangedByUserId { get; init; }
        public string? ChangedByUserName { get; init; }
        public string? Note { get; init; }
        public DateTime CreatedAt { get; init; }
    }

    private sealed class PaymentRow
    {
        public long Id { get; init; }
        public string PaymentMethod { get; init; } = "";
        public string? PayerRole { get; init; }
        public decimal Amount { get; init; }
        public long? PayosOrderCode { get; init; }
        public string TransactionStatus { get; init; } = "";
        public DateTime? PaidAt { get; init; }
        public DateTime CreatedAt { get; init; }
    }

    private sealed class IncidentRow
    {
        public long Id { get; init; }
        public string IncidentType { get; init; } = "";
        public string Description { get; init; } = "";
        public string ResolutionStatus { get; init; } = "";
        public string? ResolutionDecision { get; init; }
        public string? ResolutionNote { get; init; }
        public string ReporterName { get; init; } = "";
        public string? HandledByName { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? ResolvedAt { get; init; }
    }

    private sealed class RefundItemRow
    {
        public long Id { get; init; }
        public long? IncidentId { get; init; }
        public string SourceType { get; init; } = "";
        public string Reason { get; init; } = "";
        public decimal RequestedAmount { get; init; }
        public decimal? ProposedAmount { get; init; }
        public string RefundStatus { get; init; } = "";
        public DateTime CreatedAt { get; init; }
        public DateTime? DecidedAt { get; init; }
        public DateTime? ProcessedAt { get; init; }
    }
}

