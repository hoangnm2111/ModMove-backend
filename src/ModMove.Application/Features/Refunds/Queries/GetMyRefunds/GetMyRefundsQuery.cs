using ModMove.Application.Common.Messaging;
using ModMove.Application.Common.Models;

namespace ModMove.Application.Features.Refunds.Queries.GetMyRefunds;

/// <summary>UC-CUS-16, 23: refunds (cancellation, incident, complaint) on the signed-in customer's orders, newest first.</summary>
public sealed record GetMyRefundsQuery(int Page = 1, int PageSize = 20) : IQuery<PagedResult<MyRefundSummaryResponse>>;

/// <summary>
/// <see cref="ApprovedAmount"/> is shown only once an Administrator approved it; the Dispatcher's proposal before that is
/// not final. <see cref="WaitingForYourReply"/>: the Dispatcher asked a question the customer has not answered (7.4).
/// </summary>
public sealed record MyRefundSummaryResponse(
    long Id, long OrderId, string OrderCode, string SourceType, string RefundStatus,
    decimal RequestedAmount, decimal? ApprovedAmount, string? DecisionReason, bool WaitingForYourReply, DateTimeOffset CreatedAt);
