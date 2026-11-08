using ModMove.Application.Common.Exceptions;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;

namespace ModMove.Application.Features.Incidents;

/// <summary>Shared rules for Dispatcher incident handling and replacement driver distance checks (DECISIONS 6.4, 7.2, B5).</summary>
internal static class IncidentHandlingRules
{
    private const double EarthRadiusKm = 6371.0;

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

    /// <summary>Great-circle distance in kilometers between two WGS84 coordinates.</summary>
    public static double HaversineKm(decimal lat1, decimal lon1, decimal lat2, decimal lon2)
    {
        var dLat = ToRadians((double)(lat2 - lat1));
        var dLon = ToRadians((double)(lon2 - lon1));
        var rLat1 = ToRadians((double)lat1);
        var rLat2 = ToRadians((double)lat2);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(rLat1) * Math.Cos(rLat2) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusKm * c;
    }

    private static double ToRadians(double degrees) => degrees * (Math.PI / 180.0);
}

