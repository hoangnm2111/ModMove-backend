using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Incidents.Queries.GetReplacementDrivers;

/// <summary>
/// UC-DIS-06 (DECISIONS B5, 6.4, 7.2, GB-16): eligible online drivers near the incident who can take over the order,
/// sorted by distance ascending.
/// </summary>
public sealed record GetReplacementDriversQuery(long IncidentId) : IQuery<IReadOnlyList<ReplacementDriverResponse>>;

public sealed record ReplacementDriverResponse(
    long DriverProfileId,
    long UserId,
    string FullName,
    string PhoneNumber,
    string LicensePlate,
    long VehicleTypeId,
    string VehicleTypeName,
    decimal Latitude,
    decimal Longitude,
    DateTimeOffset LastLocationAt,
    double DistanceKm,
    decimal DepositBalance);

