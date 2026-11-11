using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Domain.Exceptions;

namespace ModMove.Application.Features.Incidents;

/// <summary>
/// DECISIONS 7.6: an Open or InProgress incident reported by the driver who now holds the order stops the trip until a
/// Dispatcher decides. A customer's incident, or one left by a previous driver, does not. The order and the incident are
/// separate aggregates, so the handler asks the repository, like COMPLAINT_OPEN (7.3).
/// </summary>
internal static class IncidentGuard
{
    public static async Task EnsureDriverHasNoOpenIncidentAsync(
        IIncidentRepository incidents, long orderId, long? driverUserId, CancellationToken cancellationToken)
    {
        if (driverUserId is { } userId && await incidents.HasOpenReportedByAsync(orderId, userId, cancellationToken))
        {
            throw new DomainException(
                "The driver reported an incident on this order. Wait for the Dispatcher's decision.", "INCIDENT_PENDING");
        }
    }
}
