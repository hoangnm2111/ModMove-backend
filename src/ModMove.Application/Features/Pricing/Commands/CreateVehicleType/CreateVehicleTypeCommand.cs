using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Pricing.Commands.CreateVehicleType;

/// <summary>
/// UC-ADM-11/15: adds a vehicle type with its capacity (GB-10, sizes in cm) and distance fares (DECISIONS B1).
/// It starts active. A name already used → 409 (vehicle_types.name is unique).
/// </summary>
public sealed record CreateVehicleTypeCommand(
    string Name,
    decimal MaxLoadWeightKg,
    int CargoBedLengthCm,
    int CargoBedWidthCm,
    int CargoBedHeightCm,
    decimal BaseFare,
    decimal PricePerKm,
    decimal MinimumFare) : ICommand<long>;
