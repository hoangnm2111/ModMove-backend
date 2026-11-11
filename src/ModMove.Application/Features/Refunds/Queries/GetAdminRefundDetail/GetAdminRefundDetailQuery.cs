using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Refunds.Queries.GetAdminRefundDetail;

/// <summary>UC-ADM-19..20: full refund details for Administrator decision and manual payout.</summary>
public sealed record GetAdminRefundDetailQuery(long RefundId) : IQuery<AdminRefundDetailResponse>;

public sealed record AdminRefundDetailResponse(
    long Id,
    long OrderId,
    string OrderCode,
    string OrderStatus,
    decimal OrderTotalFee,
    string PaymentMethod,
    string PaymentStatus,
    long? IncidentId,
    string SourceType,
    string RefundStatus,
    string? ReasonCode,
    string Reason,
    IReadOnlyList<string> EvidenceUrls,
    decimal RequestedAmount,
    decimal? ProposedAmount,
    string? ProposalReason,
    long? ProposedById,
    string? ProposedByName,
    long? ApprovedById,
    string? ApprovedByName,
    string? DecisionNote,
    string? InfoRequestNote,
    string? CustomerReply,
    string? BankReferenceCode,
    DateTimeOffset? DecidedAt,
    DateTimeOffset? ProcessedAt,
    DateTimeOffset CreatedAt,
    long CustomerId,
    string CustomerName,
    string CustomerPhone,
    string CustomerEmail,
    long? DriverProfileId,
    string? DriverName,
    string? DriverPhone,
    string? LicensePlate,
    AdminPayoutAccount? PayoutAccount,
    bool HasPayoutAccount,
    string? PayoutFailureReason);

/// <summary>R-01: full bank account shown to the Administrator to make the manual transfer (Q7).</summary>
public sealed record AdminPayoutAccount(
    string BankCode,
    string BankName,
    string AccountNumber,
    string AccountHolder,
    DateTimeOffset? InfoUpdatedAt);

