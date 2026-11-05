using ModMove.Domain.Entities;

namespace ModMove.Application.Common.Interfaces.Persistence;

public interface IIncidentRepository : IRepository<InTransitIncident>
{
    /// <summary>
    /// Open or in-progress incidents on the order other than <paramref name="excludeIncidentId"/>.
    /// Used when cancelling an order due to one incident to auto-close remaining open incidents.
    /// </summary>
    Task<List<InTransitIncident>> GetOpenForOrderAsync(
        long orderId, long excludeIncidentId, CancellationToken cancellationToken = default);
}

