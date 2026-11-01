using ModMove.Application.Common.Messaging;
using ModMove.Application.Common.Models;

namespace ModMove.Application.Features.Monitoring.Queries.ListHandledCases;

/// <summary>Filter for <see cref="ListHandledCasesQuery"/> (UC-DIS-16).</summary>
public enum HandledCaseType
{
    Incident,
    Complaint,
    IncidentRefund,
    Refund,
}

/// <summary>
/// UC-DIS-16: review the incidents and refund proposals handled by the signed-in Dispatcher, newest first.
/// </summary>
public sealed record ListHandledCasesQuery(
    HandledCaseType? Type = null,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<HandledCaseItemResponse>>;

public sealed record HandledCaseItemResponse(
    string CaseKind,
    long CaseId,
    long OrderId,
    string OrderCode,
    string SubType,
    string Status,
    string? Decision,
    string? Note,
    decimal? RequestedAmount,
    decimal? ProposedAmount,
    DateTimeOffset HandledAt);

