using Dapper;
using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Domain.Entities;

namespace ModMove.Application.Features.Refunds.Queries.GetAdminRefundDetail;

internal sealed class GetAdminRefundDetailQueryHandler(ISqlConnectionFactory connectionFactory)
    : IQueryHandler<GetAdminRefundDetailQuery, AdminRefundDetailResponse>
{
    private const string Sql = """
        SELECT r.id AS Id, r.order_id AS OrderId, o.order_code AS OrderCode, o.order_status AS OrderStatus,
               o.total_fee AS OrderTotalFee, o.payment_method AS PaymentMethod, o.payment_status AS PaymentStatus,
               r.incident_id AS IncidentId, r.source_type AS SourceType, r.refund_status AS RefundStatus,
               r.reason_code AS ReasonCode, r.reason AS Reason, r.evidence_urls AS EvidenceUrlsJson,
               r.requested_amount AS RequestedAmount, r.proposed_amount AS ProposedAmount,
               r.proposal_reason AS ProposalReason, r.proposed_by AS ProposedById, proposer.full_name AS ProposedByName,
               r.approved_by AS ApprovedById, approver.full_name AS ApprovedByName,
               r.decision_note AS DecisionNote, r.info_request_note AS InfoRequestNote, r.customer_reply AS CustomerReply,
               r.bank_reference_code AS BankReferenceCode,
               r.decided_at AS DecidedAt, r.processed_at AS ProcessedAt, r.created_at AS CreatedAt,
               cust.id AS CustomerId, cust.full_name AS CustomerName, cust.phone_number AS CustomerPhone, cust.email AS CustomerEmail,
               dp.id AS DriverProfileId, drv.full_name AS DriverName, drv.phone_number AS DriverPhone, v.license_plate AS LicensePlate
        FROM refunds r
        JOIN shipping_orders o ON o.id = r.order_id
        JOIN users cust ON cust.id = o.customer_id
        LEFT JOIN users proposer ON proposer.id = r.proposed_by
        LEFT JOIN users approver ON approver.id = r.approved_by
        LEFT JOIN driver_profiles dp ON dp.id = o.driver_profile_id
        LEFT JOIN users drv ON drv.id = dp.user_id
        LEFT JOIN vehicles v ON v.driver_profile_id = dp.id
        WHERE r.id = @RefundId
        """;

    public async Task<AdminRefundDetailResponse> Handle(GetAdminRefundDetailQuery request, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<Row>(
            new CommandDefinition(Sql, new { request.RefundId }, cancellationToken: cancellationToken))
            ?? throw new NotFoundException(nameof(Refund), request.RefundId);

        return new AdminRefundDetailResponse(
            row.Id, row.OrderId, row.OrderCode, row.OrderStatus,
            row.OrderTotalFee, row.PaymentMethod, row.PaymentStatus,
            row.IncidentId, row.SourceType, row.RefundStatus,
            row.ReasonCode, row.Reason, SqlHelpers.ParseJsonList(row.EvidenceUrlsJson),
            row.RequestedAmount, row.ProposedAmount, row.ProposalReason,
            row.ProposedById, row.ProposedByName, row.ApprovedById, row.ApprovedByName,
            row.DecisionNote, row.InfoRequestNote, row.CustomerReply,
            row.BankReferenceCode,
            SqlHelpers.ToUtc(row.DecidedAt), SqlHelpers.ToUtc(row.ProcessedAt), SqlHelpers.ToUtc(row.CreatedAt),
            row.CustomerId, row.CustomerName, row.CustomerPhone, row.CustomerEmail,
            row.DriverProfileId, row.DriverName, row.DriverPhone, row.LicensePlate);
    }

    private sealed class Row
    {
        public long Id { get; init; }
        public long OrderId { get; init; }
        public string OrderCode { get; init; } = "";
        public string OrderStatus { get; init; } = "";
        public decimal OrderTotalFee { get; init; }
        public string PaymentMethod { get; init; } = "";
        public string PaymentStatus { get; init; } = "";
        public long? IncidentId { get; init; }
        public string SourceType { get; init; } = "";
        public string RefundStatus { get; init; } = "";
        public string? ReasonCode { get; init; }
        public string Reason { get; init; } = "";
        public string? EvidenceUrlsJson { get; init; }
        public decimal RequestedAmount { get; init; }
        public decimal? ProposedAmount { get; init; }
        public string? ProposalReason { get; init; }
        public long? ProposedById { get; init; }
        public string? ProposedByName { get; init; }
        public long? ApprovedById { get; init; }
        public string? ApprovedByName { get; init; }
        public string? DecisionNote { get; init; }
        public string? InfoRequestNote { get; init; }
        public string? CustomerReply { get; init; }
        public string? BankReferenceCode { get; init; }
        public DateTime? DecidedAt { get; init; }
        public DateTime? ProcessedAt { get; init; }
        public DateTime CreatedAt { get; init; }
        public long CustomerId { get; init; }
        public string CustomerName { get; init; } = "";
        public string CustomerPhone { get; init; } = "";
        public string CustomerEmail { get; init; } = "";
        public long? DriverProfileId { get; init; }
        public string? DriverName { get; init; }
        public string? DriverPhone { get; init; }
        public string? LicensePlate { get; init; }
    }
}

