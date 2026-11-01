using Dapper;
using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Incidents.Queries.GetIncidentById;

internal sealed class GetIncidentByIdQueryHandler(ISqlConnectionFactory connectionFactory)
    : IQueryHandler<GetIncidentByIdQuery, IncidentDetailResponse>
{
    private const string IncidentSql = """
        SELECT i.id AS Id, i.order_id AS OrderId, o.order_code AS OrderCode, o.order_status AS OrderStatus,
               i.incident_type AS IncidentType, i.description AS Description, i.photo_urls AS PhotoUrlsJson,
               i.latitude AS Latitude, i.longitude AS Longitude,
               i.resolution_status AS ResolutionStatus, i.resolution_decision AS ResolutionDecision,
               i.resolution_note AS ResolutionNote,
               i.reported_by AS ReportedById, reporter.full_name AS ReporterName, reporter.role AS ReporterRole,
               i.handled_by AS HandledById, handler.full_name AS HandledByName,
               i.resolved_at AS ResolvedAt, i.created_at AS CreatedAt,
               o.pickup_address AS PickupAddress, o.dropoff_address AS DropoffAddress,
               o.payment_method AS PaymentMethod, o.payment_status AS PaymentStatus,
               o.total_fee AS TotalFee, o.delivered_at AS DeliveredAt,
               c.cargo_type AS CargoType, c.quantity AS CargoQuantity, c.weight_kg AS CargoWeightKg,
               c.length_cm AS CargoLengthCm, c.width_cm AS CargoWidthCm, c.height_cm AS CargoHeightCm,
               c.special_notes AS CargoSpecialNotes, c.photo_urls AS CargoPhotoUrlsJson,
               cust.id AS CustomerId, cust.full_name AS CustomerFullName, cust.phone_number AS CustomerPhone,
               dp.id AS DriverProfileId, drv_user.id AS DriverUserId, drv_user.full_name AS DriverFullName, drv_user.phone_number AS DriverPhone,
               v.license_plate AS LicensePlate, vt.name AS VehicleTypeName,
               pod.pickup_photo_urls AS PickupPhotoUrlsJson, pod.delivery_photo_url AS DeliveryPhotoUrl,
               pod.delivery_completion_time AS DeliveryCompletionTime, pod.proof_status AS ProofStatus
        FROM in_transit_incidents i
        JOIN shipping_orders o ON o.id = i.order_id
        LEFT JOIN cargo_details c ON c.order_id = o.id
        JOIN users cust ON cust.id = o.customer_id
        JOIN users reporter ON reporter.id = i.reported_by
        LEFT JOIN users handler ON handler.id = i.handled_by
        LEFT JOIN driver_profiles dp ON dp.id = o.driver_profile_id
        LEFT JOIN users drv_user ON drv_user.id = dp.user_id
        LEFT JOIN vehicles v ON v.driver_profile_id = dp.id
        LEFT JOIN vehicle_types vt ON vt.id = v.vehicle_type_id
        LEFT JOIN proof_of_deliveries pod ON pod.order_id = o.id
        WHERE i.id = @Id
        """;

    private const string HistorySql = """
        SELECT h.id AS Id, h.from_status AS FromStatus, h.to_status AS ToStatus,
               h.changed_by_role AS ChangedByRole, h.changed_by_user_id AS ChangedByUserId,
               u.full_name AS ChangedByUserName, h.note AS Note, h.created_at AS CreatedAt
        FROM order_status_histories h
        LEFT JOIN users u ON u.id = h.changed_by_user_id
        WHERE h.order_id = @OrderId
        ORDER BY h.id ASC
        """;

    private const string RefundSql = """
        SELECT r.id AS Id, r.source_type AS SourceType, r.reason AS Reason, r.evidence_urls AS EvidenceUrlsJson,
               r.requested_amount AS RequestedAmount, r.proposed_amount AS ProposedAmount,
               r.proposal_reason AS ProposalReason, r.proposed_by AS ProposedById,
               r.info_request_note AS InfoRequestNote, r.customer_reply AS CustomerReply,
               r.refund_status AS RefundStatus, r.created_at AS CreatedAt
        FROM refunds r
        WHERE r.incident_id = @IncidentId
        ORDER BY r.id DESC
        LIMIT 1
        """;

    public async Task<IncidentDetailResponse> Handle(GetIncidentByIdQuery request, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();

        var row = await connection.QuerySingleOrDefaultAsync<Row>(
            new CommandDefinition(IncidentSql, new { request.Id }, cancellationToken: cancellationToken))
            ?? throw new NotFoundException("InTransitIncident", request.Id);

        var historyRows = await connection.QueryAsync<HistoryRow>(
            new CommandDefinition(HistorySql, new { row.OrderId }, cancellationToken: cancellationToken));

        var refundRow = await connection.QueryFirstOrDefaultAsync<RefundRow>(
            new CommandDefinition(RefundSql, new { IncidentId = row.Id }, cancellationToken: cancellationToken));

        var cargo = row.CargoType is not null
            ? new IncidentCargoDetails(
                row.CargoType, row.CargoQuantity ?? 0, row.CargoWeightKg ?? 0,
                row.CargoLengthCm ?? 0, row.CargoWidthCm ?? 0, row.CargoHeightCm ?? 0,
                row.CargoSpecialNotes, SqlHelpers.ParseJsonList(row.CargoPhotoUrlsJson))
            : null;

        var order = new IncidentOrderDetails(
            row.OrderId, row.OrderCode, row.OrderStatus,
            row.PickupAddress, row.DropoffAddress,
            row.PaymentMethod, row.PaymentStatus, row.TotalFee,
            SqlHelpers.ToUtc(row.DeliveredAt),
            cargo);

        var customer = new IncidentCustomerDetails(row.CustomerId, row.CustomerFullName, row.CustomerPhone);

        var driver = row.DriverProfileId.HasValue && row.DriverUserId.HasValue
            ? new IncidentDriverDetails(
                row.DriverProfileId.Value, row.DriverUserId.Value,
                row.DriverFullName ?? string.Empty, row.DriverPhone ?? string.Empty,
                row.LicensePlate, row.VehicleTypeName)
            : null;

        var histories = historyRows.Select(h => new IncidentOrderStatusHistoryItem(
            h.Id, h.FromStatus, h.ToStatus, h.ChangedByRole,
            h.ChangedByUserId, h.ChangedByUserName, h.Note,
            SqlHelpers.ToUtc(h.CreatedAt))).ToList();

        var proof = row.ProofStatus is not null
            ? new IncidentProofEvidence(
                SqlHelpers.ParseJsonList(row.PickupPhotoUrlsJson),
                row.DeliveryPhotoUrl,
                SqlHelpers.ToUtc(row.DeliveryCompletionTime),
                row.ProofStatus)
            : null;

        var refund = refundRow is not null
            ? new IncidentRefundDetails(
                refundRow.Id, refundRow.SourceType, refundRow.Reason,
                SqlHelpers.ParseJsonList(refundRow.EvidenceUrlsJson),
                refundRow.RequestedAmount, refundRow.ProposedAmount,
                refundRow.ProposalReason, refundRow.ProposedById,
                refundRow.InfoRequestNote, refundRow.CustomerReply,
                refundRow.RefundStatus, SqlHelpers.ToUtc(refundRow.CreatedAt))
            : null;

        return new IncidentDetailResponse(
            row.Id, row.OrderId, row.OrderCode, row.OrderStatus,
            row.IncidentType, row.Description,
            SqlHelpers.ParseJsonList(row.PhotoUrlsJson),
            row.Latitude, row.Longitude,
            row.ResolutionStatus, row.ResolutionDecision, row.ResolutionNote,
            row.ReportedById, row.ReporterName, row.ReporterRole,
            row.HandledById, row.HandledByName,
            SqlHelpers.ToUtc(row.ResolvedAt),
            SqlHelpers.ToUtc(row.CreatedAt),
            order, customer, driver, proof, histories, refund);
    }

    private sealed class Row
    {
        public long Id { get; init; }
        public long OrderId { get; init; }
        public string OrderCode { get; init; } = "";
        public string OrderStatus { get; init; } = "";
        public string IncidentType { get; init; } = "";
        public string Description { get; init; } = "";
        public string? PhotoUrlsJson { get; init; }
        public decimal? Latitude { get; init; }
        public decimal? Longitude { get; init; }
        public string ResolutionStatus { get; init; } = "";
        public string? ResolutionDecision { get; init; }
        public string? ResolutionNote { get; init; }
        public long ReportedById { get; init; }
        public string ReporterName { get; init; } = "";
        public string ReporterRole { get; init; } = "";
        public long? HandledById { get; init; }
        public string? HandledByName { get; init; }
        public DateTime? ResolvedAt { get; init; }
        public DateTime CreatedAt { get; init; }
        public string PickupAddress { get; init; } = "";
        public string DropoffAddress { get; init; } = "";
        public string PaymentMethod { get; init; } = "";
        public string PaymentStatus { get; init; } = "";
        public decimal TotalFee { get; init; }
        public DateTime? DeliveredAt { get; init; }
        public string? CargoType { get; init; }
        public int? CargoQuantity { get; init; }
        public decimal? CargoWeightKg { get; init; }
        public int? CargoLengthCm { get; init; }
        public int? CargoWidthCm { get; init; }
        public int? CargoHeightCm { get; init; }
        public string? CargoSpecialNotes { get; init; }
        public string? CargoPhotoUrlsJson { get; init; }
        public long CustomerId { get; init; }
        public string CustomerFullName { get; init; } = "";
        public string CustomerPhone { get; init; } = "";
        public long? DriverProfileId { get; init; }
        public long? DriverUserId { get; init; }
        public string? DriverFullName { get; init; }
        public string? DriverPhone { get; init; }
        public string? LicensePlate { get; init; }
        public string? VehicleTypeName { get; init; }
        public string? PickupPhotoUrlsJson { get; init; }
        public string? DeliveryPhotoUrl { get; init; }
        public DateTime? DeliveryCompletionTime { get; init; }
        public string? ProofStatus { get; init; }
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

    private sealed class RefundRow
    {
        public long Id { get; init; }
        public string SourceType { get; init; } = "";
        public string Reason { get; init; } = "";
        public string? EvidenceUrlsJson { get; init; }
        public decimal RequestedAmount { get; init; }
        public decimal? ProposedAmount { get; init; }
        public string? ProposalReason { get; init; }
        public long? ProposedById { get; init; }
        public string? InfoRequestNote { get; init; }
        public string? CustomerReply { get; init; }
        public string RefundStatus { get; init; } = "";
        public DateTime CreatedAt { get; init; }
    }
}
