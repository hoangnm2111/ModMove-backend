using Dapper;
using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Domain.Enums;

namespace ModMove.Application.Features.Refunds.Queries.GetComplaintById;

internal sealed class GetComplaintByIdQueryHandler(ISqlConnectionFactory connectionFactory)
    : IQueryHandler<GetComplaintByIdQuery, ComplaintDetailResponse>
{
    private const string ComplaintSql = """
        SELECT r.id AS Id, r.order_id AS OrderId, o.order_code AS OrderCode, o.order_status AS OrderStatus,
               r.reason AS Reason, r.evidence_urls AS EvidenceUrlsJson,
               r.requested_amount AS RequestedAmount, r.proposed_amount AS ProposedAmount,
               r.proposal_reason AS ProposalReason, r.proposed_by AS ProposedById,
               proposer.full_name AS ProposedByName,
               r.info_request_note AS InfoRequestNote, r.customer_reply AS CustomerReply,
               r.refund_status AS RefundStatus, r.bank_reference_code AS BankReferenceCode,
               r.decided_at AS DecidedAt, r.processed_at AS ProcessedAt, r.created_at AS CreatedAt,
               o.pickup_address AS PickupAddress, o.dropoff_address AS DropoffAddress,
               o.payment_method AS PaymentMethod, o.payment_status AS PaymentStatus,
               o.total_fee AS TotalFee, o.delivered_at AS DeliveredAt, o.completed_at AS CompletedAt,
               c.cargo_type AS CargoType, c.quantity AS CargoQuantity, c.weight_kg AS CargoWeightKg,
               c.length_cm AS CargoLengthCm, c.width_cm AS CargoWidthCm, c.height_cm AS CargoHeightCm,
               c.special_notes AS CargoSpecialNotes,
               cust.id AS CustomerId, cust.full_name AS CustomerName, cust.phone_number AS CustomerPhone,
               dp.id AS DriverProfileId, drv_user.id AS DriverUserId, drv_user.full_name AS DriverFullName, drv_user.phone_number AS DriverPhone,
               v.license_plate AS LicensePlate, vt.name AS VehicleTypeName,
               pod.pickup_photo_urls AS PickupPhotoUrlsJson, pod.delivery_photo_url AS DeliveryPhotoUrl,
               pod.delivery_completion_time AS DeliveryCompletionTime, pod.proof_status AS ProofStatus,
               rtg.rating_stars AS RatingStars, rtg.review_comment AS ReviewComment, rtg.created_at AS RatingCreatedAt
        FROM refunds r
        JOIN shipping_orders o ON o.id = r.order_id
        JOIN users cust ON cust.id = o.customer_id
        LEFT JOIN users proposer ON proposer.id = r.proposed_by
        LEFT JOIN cargo_details c ON c.order_id = o.id
        LEFT JOIN driver_profiles dp ON dp.id = o.driver_profile_id
        LEFT JOIN users drv_user ON drv_user.id = dp.user_id
        LEFT JOIN vehicles v ON v.driver_profile_id = dp.id
        LEFT JOIN vehicle_types vt ON vt.id = v.vehicle_type_id
        LEFT JOIN proof_of_deliveries pod ON pod.order_id = o.id
        LEFT JOIN ratings rtg ON rtg.order_id = o.id
        WHERE r.id = @Id AND r.source_type = 'Complaint'
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

    private const string IncidentsSql = """
        SELECT i.id AS Id, i.incident_type AS IncidentType, i.description AS Description,
               i.resolution_status AS ResolutionStatus, i.resolution_decision AS ResolutionDecision,
               i.created_at AS CreatedAt
        FROM in_transit_incidents i
        WHERE i.order_id = @OrderId
        ORDER BY i.id ASC
        """;

    public async Task<ComplaintDetailResponse> Handle(GetComplaintByIdQuery request, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();

        var row = await connection.QuerySingleOrDefaultAsync<Row>(
            new CommandDefinition(ComplaintSql, new { request.Id }, cancellationToken: cancellationToken))
            ?? throw new NotFoundException("Refund", request.Id);

        var historyRows = await connection.QueryAsync<HistoryRow>(
            new CommandDefinition(HistorySql, new { row.OrderId }, cancellationToken: cancellationToken));

        var incidentRows = await connection.QueryAsync<IncidentRow>(
            new CommandDefinition(IncidentsSql, new { row.OrderId }, cancellationToken: cancellationToken));

        var stage = row.RefundStatus != nameof(RefundStatus.Requested)
            ? ComplaintStage.Closed
            : row.ProposedById.HasValue
                ? ComplaintStage.ProposedToAdmin
                : row.CustomerReply is not null
                    ? ComplaintStage.CustomerReplied
                    : row.InfoRequestNote is not null
                        ? ComplaintStage.WaitingForCustomer
                        : ComplaintStage.NeedsReview;

        var cargo = row.CargoType is not null
            ? new ComplaintCargoDetails(
                row.CargoType, row.CargoQuantity ?? 0, row.CargoWeightKg ?? 0,
                row.CargoLengthCm ?? 0, row.CargoWidthCm ?? 0, row.CargoHeightCm ?? 0,
                row.CargoSpecialNotes)
            : null;

        var order = new ComplaintOrderDetails(
            row.OrderId, row.OrderCode, row.OrderStatus,
            row.PickupAddress, row.DropoffAddress,
            row.PaymentMethod, row.PaymentStatus, row.TotalFee,
            SqlHelpers.ToUtc(row.DeliveredAt),
            SqlHelpers.ToUtc(row.CompletedAt),
            cargo);

        var customer = new ComplaintCustomerDetails(row.CustomerId, row.CustomerName, row.CustomerPhone);

        var driver = row.DriverProfileId.HasValue && row.DriverUserId.HasValue
            ? new ComplaintDriverDetails(
                row.DriverProfileId.Value, row.DriverUserId.Value,
                row.DriverFullName ?? string.Empty, row.DriverPhone ?? string.Empty,
                row.LicensePlate, row.VehicleTypeName)
            : null;

        var evidence = row.ProofStatus is not null
            ? new ComplaintVerificationEvidence(
                SqlHelpers.ParseJsonList(row.PickupPhotoUrlsJson),
                row.DeliveryPhotoUrl,
                SqlHelpers.ToUtc(row.DeliveryCompletionTime),
                row.ProofStatus)
            : null;

        var rating = row.RatingStars.HasValue
            ? new ComplaintRatingDetails(
                row.RatingStars.Value,
                row.ReviewComment,
                SqlHelpers.ToUtc(row.RatingCreatedAt ?? row.CreatedAt))
            : null;

        var incidents = incidentRows.Select(i => new ComplaintIncidentSummary(
            i.Id, i.IncidentType, i.Description, i.ResolutionStatus,
            i.ResolutionDecision, SqlHelpers.ToUtc(i.CreatedAt))).ToList();

        var histories = historyRows.Select(h => new ComplaintOrderStatusHistoryItem(
            h.Id, h.FromStatus, h.ToStatus, h.ChangedByRole,
            h.ChangedByUserId, h.ChangedByUserName, h.Note,
            SqlHelpers.ToUtc(h.CreatedAt))).ToList();

        return new ComplaintDetailResponse(
            row.Id, row.OrderId, row.OrderCode, row.OrderStatus,
            row.Reason, SqlHelpers.ParseJsonList(row.EvidenceUrlsJson),
            row.RequestedAmount, row.ProposedAmount, row.ProposalReason,
            row.ProposedById, row.ProposedByName,
            row.InfoRequestNote, row.CustomerReply,
            stage.ToString(), row.RefundStatus,
            row.BankReferenceCode,
            SqlHelpers.ToUtc(row.DecidedAt),
            SqlHelpers.ToUtc(row.ProcessedAt),
            SqlHelpers.ToUtc(row.CreatedAt),
            order, customer, driver, evidence, rating, incidents, histories);
    }

    private sealed record Row(
        long Id, long OrderId, string OrderCode, string OrderStatus,
        string Reason, string? EvidenceUrlsJson,
        decimal RequestedAmount, decimal? ProposedAmount,
        string? ProposalReason, long? ProposedById, string? ProposedByName,
        string? InfoRequestNote, string? CustomerReply,
        string RefundStatus, string? BankReferenceCode,
        DateTime? DecidedAt, DateTime? ProcessedAt, DateTime CreatedAt,
        string PickupAddress, string DropoffAddress, string PaymentMethod, string PaymentStatus,
        decimal TotalFee, DateTime? DeliveredAt, DateTime? CompletedAt,
        string? CargoType, int? CargoQuantity, decimal? CargoWeightKg,
        int? CargoLengthCm, int? CargoWidthCm, int? CargoHeightCm, string? CargoSpecialNotes,
        long CustomerId, string CustomerName, string CustomerPhone,
        long? DriverProfileId, long? DriverUserId, string? DriverFullName, string? DriverPhone,
        string? LicensePlate, string? VehicleTypeName,
        string? PickupPhotoUrlsJson, string? DeliveryPhotoUrl, DateTime? DeliveryCompletionTime, string? ProofStatus,
        int? RatingStars, string? ReviewComment, DateTime? RatingCreatedAt);

    private sealed record HistoryRow(
        long Id, string? FromStatus, string ToStatus, string ChangedByRole,
        long? ChangedByUserId, string? ChangedByUserName, string? Note, DateTime CreatedAt);

    private sealed record IncidentRow(
        long Id, string IncidentType, string Description,
        string ResolutionStatus, string? ResolutionDecision, DateTime CreatedAt);
}
