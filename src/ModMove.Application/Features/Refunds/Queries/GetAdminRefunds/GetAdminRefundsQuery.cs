using ModMove.Application.Common.Messaging;
using ModMove.Application.Common.Models;
using ModMove.Domain.Enums;

namespace ModMove.Application.Features.Refunds.Queries.GetAdminRefunds;

/// <summary>UC-ADM-18..20: paginated refund list for Administrator review and payout.</summary>
public sealed record GetAdminRefundsQuery(
    int Page = 1,
    int PageSize = 20,
    RefundStatus? Status = null,
    RefundSourceType? SourceType = null,
    string? Search = null) : IQuery<PagedResult<AdminRefundDto>>;

public sealed record AdminRefundDto(
    long Id,
    long OrderId,
    string OrderCode,
    string CustomerName,
    string CustomerPhone,
    string SourceType,
    string RefundStatus,
    decimal RequestedAmount,
    decimal? ProposedAmount,
    string? ReasonCode,
    string Reason,
    string? ProposalReason,
    string? ProposedByName,
    string? DecisionNote,
    string? BankReferenceCode,
    DateTimeOffset? DecidedAt,
    DateTimeOffset? ProcessedAt,
    DateTimeOffset CreatedAt,
    bool HasPayoutAccount,
    string? PayoutFailureReason);

