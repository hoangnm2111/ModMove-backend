using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Refunds.Queries.GetMyRefundDetail;

/// <summary>UC-CUS-16, 23: one refund on the customer's own order; someone else's is 404.</summary>
public sealed record GetMyRefundDetailQuery(long RefundId) : IQuery<MyRefundDetailResponse>;

/// <summary>
/// <see cref="ApprovedAmount"/> is null until an Administrator approves. <see cref="InfoRequestNote"/> is the
/// Dispatcher's question; <see cref="CustomerReply"/> the answer (7.4).
/// </summary>
public sealed record MyRefundDetailResponse(
    long Id, long OrderId, string OrderCode, long? IncidentId, string SourceType, string RefundStatus, string Reason,
    IReadOnlyList<string> EvidenceUrls, decimal RequestedAmount, decimal? ApprovedAmount,
    string? InfoRequestNote, string? CustomerReply, bool WaitingForYourReply,
    DateTimeOffset CreatedAt, DateTimeOffset? DecidedAt, DateTimeOffset? ProcessedAt);
