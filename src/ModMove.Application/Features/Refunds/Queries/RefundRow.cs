namespace ModMove.Application.Features.Refunds.Queries;

/// <summary>One refund of the customer as read by Dapper; column aliases match the property names.</summary>
internal sealed class RefundRow
{
    /// <summary>Joined with the order so only the caller's refunds come back (IDOR, C10).</summary>
    public const string FromCustomerRefunds = """
        FROM refunds r
        JOIN shipping_orders o ON o.id = r.order_id
        WHERE o.customer_id = @CustomerId
        """;

    public const string Columns = """
        r.id AS Id, r.order_id AS OrderId, o.order_code AS OrderCode, r.incident_id AS IncidentId,
        r.source_type AS SourceType, r.refund_status AS RefundStatus, r.reason AS Reason, r.reason_code AS ReasonCode, r.evidence_urls AS EvidenceUrls,
        r.requested_amount AS RequestedAmount, r.proposed_amount AS ProposedAmount,
        r.info_request_note AS InfoRequestNote, r.customer_reply AS CustomerReply, r.decision_note AS DecisionReason,
        r.decided_at AS DecidedAt, r.processed_at AS ProcessedAt, r.created_at AS CreatedAt,
        r.payout_bank_code AS PayoutBankCode, r.payout_bank_name AS PayoutBankName,
        r.payout_account_number AS PayoutAccountNumber, r.payout_account_holder AS PayoutAccountHolder,
        r.payout_failure_reason AS PayoutFailureReason, r.bank_reference_code AS BankReferenceCode
        """;

    public long Id { get; init; }
    public long OrderId { get; init; }
    public string OrderCode { get; init; } = "";
    public long? IncidentId { get; init; }
    public string SourceType { get; init; } = "";
    public string RefundStatus { get; init; } = "";
    public string Reason { get; init; } = "";
    public string? ReasonCode { get; init; }
    public string? EvidenceUrls { get; init; }
    public decimal RequestedAmount { get; init; }
    public decimal? ProposedAmount { get; init; }
    public string? InfoRequestNote { get; init; }
    public string? CustomerReply { get; init; }

    /// <summary>CUS-23: the Administrator's note, set only when they decide. The Dispatcher's proposal_reason stays internal.</summary>
    public string? DecisionReason { get; init; }

    public DateTime? DecidedAt { get; init; }
    public DateTime? ProcessedAt { get; init; }
    public DateTime CreatedAt { get; init; }

    // R-01. The full number stays in this row; only PayoutAccount (masked) goes into a response.
    public string? PayoutBankCode { get; init; }
    public string? PayoutBankName { get; init; }
    public string? PayoutAccountNumber { get; init; }
    public string? PayoutAccountHolder { get; init; }
    public string? PayoutFailureReason { get; init; }
    public string? BankReferenceCode { get; init; }

    /// <summary>The customer can still give or fix the account: none given yet and the refund is not closed.</summary>
    public bool NeedsPayoutAccount => PayoutAccountNumber is null && RefundStatus is "Requested" or "Approved" or "Failed";

    public PayoutAccountView? PayoutAccount => PayoutAccountNumber is null
        ? null
        : new PayoutAccountView(PayoutBankCode ?? "", PayoutBankName ?? "", PayoutAccountView.Mask(PayoutAccountNumber), PayoutAccountHolder ?? "");

    /// <summary>A Dispatcher's proposal is not final; the customer sees an amount once an Administrator approved it.</summary>
    public decimal? ApprovedAmount => RefundStatus is "Approved" or "Processed" or "Failed" ? ProposedAmount : null;

    public bool WaitingForReply => RefundStatus == "Requested" && InfoRequestNote is not null && CustomerReply is null;
}
