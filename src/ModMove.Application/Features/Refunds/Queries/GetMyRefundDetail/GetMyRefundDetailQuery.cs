using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Refunds.Queries.GetMyRefundDetail;

/// <summary>UC-CUS-16, 23: one refund on the customer's own order; someone else's is 404.</summary>
public sealed record GetMyRefundDetailQuery(long RefundId) : IQuery<MyRefundDetailResponse>;

/// <summary>
/// <see cref="ApprovedAmount"/> is null until an Administrator approves. <see cref="InfoRequestNote"/> is the
/// Dispatcher's question; <see cref="CustomerReply"/> the answer (7.4). <see cref="DecisionReason"/> is the Administrator's
/// reason, null until they decide (CUS-23).
/// R-01: <see cref="PayoutAccount"/> is masked; <see cref="NeedsPayoutAccount"/> tells the app to ask for one;
/// <see cref="PayoutFailureReason"/> is why the last transfer failed; <see cref="BankReferenceCode"/> shows once paid;
/// <see cref="SuggestedPayoutAccount"/> is the account the customer paid from through PayOS, to confirm (Q3).
/// </summary>
public sealed record MyRefundDetailResponse(
    long Id, long OrderId, string OrderCode, long? IncidentId, string SourceType, string RefundStatus, string Reason, string? ReasonCode,
    IReadOnlyList<string> EvidenceUrls, decimal RequestedAmount, decimal? ApprovedAmount, string? DecisionReason,
    string? InfoRequestNote, string? CustomerReply, bool WaitingForYourReply,
    DateTimeOffset CreatedAt, DateTimeOffset? DecidedAt, DateTimeOffset? ProcessedAt,
    PayoutAccountView? PayoutAccount, bool NeedsPayoutAccount, string? PayoutFailureReason, string? BankReferenceCode,
    PayoutAccountView? SuggestedPayoutAccount);
