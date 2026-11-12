using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;

namespace ModMove.Application.Features.Incidents;

/// <summary>Shared rules for Dispatcher incident handling and the point replacement drivers are measured from (DECISIONS 7.2, B5).</summary>
internal static class IncidentHandlingRules
{
    /// <summary>
    /// Refuses with 409 when another Dispatcher already took over the incident (<see cref="IncidentResolutionStatus.InProgress"/>).
    /// An <see cref="IncidentResolutionStatus.Open"/> incident can be taken or decided directly.
    /// </summary>
    public static void EnsureCanHandle(InTransitIncident incident, long dispatcherId)
    {
        if (incident.ResolutionStatus == IncidentResolutionStatus.InProgress
            && incident.HandledBy is { } ownerId
            && ownerId != dispatcherId)
        {
            throw new ConflictException("This incident is already being handled by another dispatcher.");
        }
    }

    /// <summary>Whether a driver reported the incident: only then is its point the vehicle's and its reporter the driver it concerns.</summary>
    public static async Task<bool> ReportedByDriverAsync(InTransitIncident incident, IUserRepository users, CancellationToken cancellationToken) =>
        (await users.GetByIdAsync(incident.ReportedBy, cancellationToken))?.Role == UserRole.Driver;

    /// <summary>
    /// Where the vehicle is, to look for a replacement driver near it (DECISIONS 7.2). A driver's incident carries the
    /// vehicle's point; a customer's carries the customer's phone and a Dispatcher's none (Q6b), so for those the current
    /// driver's last GPS is used instead. Either way, without a full point the order's pickup point is used.
    /// </summary>
    public static (decimal Latitude, decimal Longitude) ReferencePoint(
        InTransitIncident incident, bool reportedByDriver, ShippingOrder order, DriverProfile? currentDriver) =>
        reportedByDriver
            ? ReferencePoint(incident.Latitude, incident.Longitude, order.PickupLatitude, order.PickupLongitude)
            : ReferencePoint(currentDriver?.LastLatitude, currentDriver?.LastLongitude, order.PickupLatitude, order.PickupLongitude);

    /// <summary>Same rule for a row read with Dapper: a point with only one coordinate is not used.</summary>
    public static (decimal Latitude, decimal Longitude) ReferencePoint(
        decimal? incidentLatitude, decimal? incidentLongitude, decimal pickupLatitude, decimal pickupLongitude) =>
        incidentLatitude is { } lat && incidentLongitude is { } lon
            ? (lat, lon)
            : (pickupLatitude, pickupLongitude);
}

