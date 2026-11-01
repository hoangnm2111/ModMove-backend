using ModMove.Application.Common.Messaging;
using ModMove.Application.Common.Models;

namespace ModMove.Application.Features.DriverOnboarding.Queries.GetPendingApplications;

/// <summary>
/// Screen #33 (UC-ADM-08): FIFO queue of pending driver applications waiting for admin approval.
/// </summary>
public sealed record GetPendingApplicationsQuery(int Page = 1, int PageSize = 20)
    : IQuery<PagedResult<PendingApplicationDto>>;
