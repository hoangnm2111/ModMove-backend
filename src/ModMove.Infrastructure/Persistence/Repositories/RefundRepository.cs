using Microsoft.EntityFrameworkCore;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;

namespace ModMove.Infrastructure.Persistence.Repositories;

public sealed class RefundRepository(ApplicationDbContext dbContext)
    : Repository<Refund>(dbContext), IRefundRepository
{
    public Task<bool> HasOpenComplaintAsync(long orderId, CancellationToken cancellationToken = default) =>
        Set.AnyAsync(
            r => r.OrderId == orderId && r.SourceType == RefundSourceType.Complaint
                && (r.RefundStatus == RefundStatus.Requested || r.RefundStatus == RefundStatus.Approved),
            cancellationToken);

    public Task<bool> HasOpenRefundForIncidentAsync(long incidentId, CancellationToken cancellationToken = default) =>
        Set.AnyAsync(
            r => r.IncidentId == incidentId
                && (r.RefundStatus == RefundStatus.Requested || r.RefundStatus == RefundStatus.Approved),
            cancellationToken);
}
