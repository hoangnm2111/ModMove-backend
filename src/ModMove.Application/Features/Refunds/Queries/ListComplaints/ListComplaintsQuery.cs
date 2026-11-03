using ModMove.Application.Common.Messaging;
using ModMove.Application.Common.Models;
using ModMove.Domain.Enums;

namespace ModMove.Application.Features.Refunds.Queries.ListComplaints;

/// <summary>
/// UC-DIS-11: list complaints filed by customers within 24 hours of delivery. Filterable by operational stage
/// and order code.
/// </summary>
public sealed record ListComplaintsQuery(
    ComplaintStage? Stage,
    string? Search,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<ComplaintListItemResponse>>;

public sealed record ComplaintListItemResponse(
    long Id,
    long OrderId,
    string OrderCode,
    string CustomerName,
    string Reason,
    decimal RequestedAmount,
    decimal? ProposedAmount,
    string Stage,
    string RefundStatus,
    DateTimeOffset? DeliveredAt,
    DateTimeOffset CreatedAt,
    string? ReasonCode = null,
    string? ReasonCodeName = null);
