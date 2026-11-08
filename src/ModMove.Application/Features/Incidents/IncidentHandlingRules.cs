using ModMove.Application.Common.Exceptions;
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

    /// <summary>Incident GPS point when available; falls back to the order's pickup point when reported without coordinates.</summary>
    public static (decimal Latitude, decimal Longitude) ReferencePoint(
        InTransitIncident incident, decimal pickupLatitude, decimal pickupLongitude) =>
        ReferencePoint(incident.Latitude, incident.Longitude, pickupLatitude, pickupLongitude);

    /// <summary>Same rule for a row read with Dapper: a point with only one coordinate is not used.</summary>
    public static (decimal Latitude, decimal Longitude) ReferencePoint(
        decimal? incidentLatitude, decimal? incidentLongitude, decimal pickupLatitude, decimal pickupLongitude) =>
        incidentLatitude is { } lat && incidentLongitude is { } lon
            ? (lat, lon)
            : (pickupLatitude, pickupLongitude);
}

