using Microsoft.EntityFrameworkCore;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;

namespace ModMove.Infrastructure.Persistence.Repositories;

public sealed class IncidentRepository(ApplicationDbContext dbContext)
    : Repository<InTransitIncident>(dbContext), IIncidentRepository
{
    public Task<List<InTransitIncident>> GetOpenForOrderAsync(
        long orderId, long excludeIncidentId, CancellationToken cancellationToken = default) =>
        Set.Where(i => i.OrderId == orderId
                && i.Id != excludeIncidentId
                && (i.ResolutionStatus == IncidentResolutionStatus.Open || i.ResolutionStatus == IncidentResolutionStatus.InProgress))
            .ToListAsync(cancellationToken);
}

