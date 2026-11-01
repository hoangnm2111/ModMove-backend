using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Monitoring.Queries.ListOnlineDrivers;

/// <summary>
/// UC-DIS-01 Step 4 (DECISIONS 6.4, B12): initial snapshot of all Online, Approved, Active drivers whose GPS location
/// was updated within the last 5 minutes (both idle and on an active order). Live updates stream via SignalR <c>LocationUpdated</c>.
/// </summary>
public sealed record ListOnlineDriversQuery : IQuery<IReadOnlyList<OnlineDriverMapItemResponse>>;

public sealed record OnlineDriverMapItemResponse(
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
    long? ActiveOrderId,
    string? ActiveOrderCode,
    string? ActiveOrderStatus);

