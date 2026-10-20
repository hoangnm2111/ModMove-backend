using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Catalog.Queries.GetVehicleTypes;

public sealed record GetVehicleTypesQuery : IQuery<IReadOnlyList<VehicleTypeResponse>>;

public sealed record VehicleTypeResponse(
    long Id,
    string Name,
    decimal MaxLoadWeightKg,
    int CargoBedLengthCm,
    int CargoBedWidthCm,
    int CargoBedHeightCm,
    decimal BaseFare,
    decimal PricePerKm,
    decimal MinimumFare);
