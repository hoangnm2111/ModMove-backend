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

    public Task<Refund?> GetOpenForIncidentAsync(long incidentId, CancellationToken cancellationToken = default) =>
        Set.OrderByDescending(r => r.Id).FirstOrDefaultAsync(
            r => r.IncidentId == incidentId
                && (r.RefundStatus == RefundStatus.Requested || r.RefundStatus == RefundStatus.Approved),
            cancellationToken);

    public Task<decimal> GetClaimedTotalForOrderAsync(long orderId, CancellationToken cancellationToken = default) =>
        Set.Where(r => r.OrderId == orderId
                && (r.RefundStatus == RefundStatus.Requested || r.RefundStatus == RefundStatus.Approved || r.RefundStatus == RefundStatus.Processed))
            .SumAsync(r => r.ProposedAmount ?? r.RequestedAmount, cancellationToken);

    public Task<bool> HasOpenForCustomerAsync(long customerId, CancellationToken cancellationToken = default) =>
        Set.Where(r => r.RefundStatus == RefundStatus.Requested || r.RefundStatus == RefundStatus.Approved)
            .Join(DbContext.Set<ShippingOrder>().Where(o => o.CustomerId == customerId), r => r.OrderId, o => o.Id, (r, _) => r.Id)
            .AnyAsync(cancellationToken);
}
